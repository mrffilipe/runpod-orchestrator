using RunpodOrchestrator.Application.Common.RunPod;
using RunpodOrchestrator.Application.Exceptions;
using RunpodOrchestrator.Application.Ports.PodLifecycle;
using RunpodOrchestrator.Application.Ports.RunPod;
using RunpodOrchestrator.Domain.Exceptions;
using RunpodOrchestrator.Infrastructure.Configurations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RunpodOrchestrator.Infrastructure.Services.PodLifecycle;

public sealed class ManagedPodResolver : IManagedPodResolver
{
    private static readonly HashSet<string> AvailableStockStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "High", "Medium", "Low" };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IManagedPodCleanupService _managedPodCleanupService;
    private readonly RunPodOptions _options;
    private readonly ILogger<ManagedPodResolver> _logger;
    private readonly SemaphoreSlim _resolveSemaphore = new(1, 1);

    private string? _cachedPodId;

    public ManagedPodResolver(
        IServiceScopeFactory scopeFactory,
        IManagedPodCleanupService managedPodCleanupService,
        IOptions<RunPodOptions> options,
        ILogger<ManagedPodResolver> logger)
    {
        _scopeFactory = scopeFactory;
        _managedPodCleanupService = managedPodCleanupService;
        _options = options.Value;
        _logger = logger;
    }

    public void InvalidateCache()
    {
        _cachedPodId = null;
        _logger.LogInformation("Managed pod cache invalidated.");
    }

    public async Task<string> GetManagedPodIdAsync(CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(_cachedPodId))
        {
            return _cachedPodId;
        }

        await _resolveSemaphore.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            if (!string.IsNullOrWhiteSpace(_cachedPodId))
            {
                return _cachedPodId;
            }

            _cachedPodId = await ResolveManagedPodIdAsync(ct).ConfigureAwait(false);
            return _cachedPodId;
        }
        finally
        {
            _resolveSemaphore.Release();
        }
    }

    private async Task<string> ResolveManagedPodIdAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var runPodClient = scope.ServiceProvider.GetRequiredService<IRunPodApiClient>();

            var podList = await runPodClient.ListPodsAsync(ct).ConfigureAwait(false);
            var managedPods = podList.Pods
                .Where(pod => ManagedPodFilter.IsManaged(pod, _options))
                .ToList();

            var runningPod = SelectBestPod(managedPods.Where(p => p.IsRunning).ToList());
            if (runningPod is not null)
            {
                _logger.LogInformation(
                    "Reusing running managed pod {PodId} ({PodName}, GPU: {GpuTypeId}).",
                    runningPod.Id,
                    runningPod.Name,
                    runningPod.GpuTypeId);
                return runningPod.Id;
            }

            var stoppedPod = SelectBestPod(managedPods.Where(p => !p.IsRunning).ToList());
            if (stoppedPod is not null)
            {
                _logger.LogInformation(
                    "Reusing stopped managed pod {PodId} ({PodName}, GPU: {GpuTypeId}).",
                    stoppedPod.Id,
                    stoppedPod.Name,
                    stoppedPod.GpuTypeId);
                return stoppedPod.Id;
            }

            _logger.LogInformation(
                "No managed pod named {PodName} found. Cleaning up leftovers before deploy.",
                _options.PodName);

            await _managedPodCleanupService.TerminateAllManagedPodsAsync(ct: ct).ConfigureAwait(false);

            var deployedPodId = await DeployPodWithGpuFallbackAsync(runPodClient, ct).ConfigureAwait(false);
            return deployedPodId;
        }
        catch (ManagedPodResolutionException)
        {
            throw;
        }
        catch (RunPodApiException ex)
        {
            _logger.LogError(ex, "RunPod API error while resolving managed pod.");
            throw new ManagedPodResolutionException(ApplicationErrorMessages.ManagedPod.RESOLUTION_FAILED, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while resolving managed pod.");
            throw new ManagedPodResolutionException(ApplicationErrorMessages.ManagedPod.RESOLUTION_FAILED, ex);
        }
    }

    private PodSummaryResponse? SelectBestPod(IReadOnlyList<PodSummaryResponse> candidates)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        foreach (var preferredGpu in _options.PreferredGpuTypeIds)
        {
            if (string.IsNullOrWhiteSpace(preferredGpu))
            {
                continue;
            }

            var match = candidates.FirstOrDefault(pod =>
                !string.IsNullOrWhiteSpace(pod.GpuTypeId) &&
                string.Equals(pod.GpuTypeId, preferredGpu, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                return match;
            }
        }

        return candidates[0];
    }

    private async Task<string> DeployPodWithGpuFallbackAsync(IRunPodApiClient runPodClient, CancellationToken ct)
    {
        var availableGpus = await GetAvailableGpusInPriorityOrderAsync(runPodClient, ct).ConfigureAwait(false);
        RunPodApiException? lastCapacityError = null;

        foreach (var gpuTypeId in availableGpus)
        {
            var deployRequest = BuildDeployRequest(gpuTypeId);

            try
            {
                var deployed = await runPodClient.DeployPodAsync(deployRequest, ct).ConfigureAwait(false);

                _logger.LogInformation(
                    "Created new managed pod {PodId} with GPU {GpuTypeId}.",
                    deployed.Id,
                    gpuTypeId);

                return deployed.Id;
            }
            catch (RunPodApiException ex) when (IsDeployCapacityError(ex))
            {
                lastCapacityError = ex;
                _logger.LogWarning(
                    ex,
                    "Deploy on GPU {GpuTypeId} failed due to capacity or region constraints. Trying next preferred type.",
                    gpuTypeId);
            }
        }

        if (lastCapacityError is not null)
        {
            throw lastCapacityError;
        }

        throw new ManagedPodResolutionException(ApplicationErrorMessages.RunPod.NO_GPU_AVAILABLE);
    }

    private PodDeployRequest BuildDeployRequest(string gpuTypeId)
    {
        var networkVolumeId = IsOptionalConfigEmpty(_options.NetworkVolumeId)
            ? null
            : _options.NetworkVolumeId;
        var environment = BuildPodDeployEnvironment();

        if (!IsOptionalConfigEmpty(_options.PodTemplateId))
        {
            return new PodDeployRequest
            {
                GpuTypeId = gpuTypeId,
                Name = _options.PodName,
                GpuCount = _options.GpuCount,
                TemplateId = _options.PodTemplateId,
                NetworkVolumeId = networkVolumeId,
                Environment = environment
            };
        }

        return new PodDeployRequest
        {
            GpuTypeId = gpuTypeId,
            Name = _options.PodName,
            GpuCount = _options.GpuCount,
            ImageName = _options.PodImageName,
            Ports = _options.PodPorts,
            VolumeMountPath = _options.VolumeMountPath,
            VolumeInGb = _options.VolumeInGb,
            ContainerDiskInGb = _options.ContainerDiskInGb,
            NetworkVolumeId = networkVolumeId,
            Environment = environment
        };
    }

    private static bool IsDeployCapacityError(RunPodApiException ex) =>
        ex.Message.Contains("no longer any instances available", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("no instances available", StringComparison.OrdinalIgnoreCase);

    private async Task<IReadOnlyList<string>> GetAvailableGpusInPriorityOrderAsync(
        IRunPodApiClient runPodClient,
        CancellationToken ct)
    {
        var availableGpus = new List<string>();

        foreach (var gpuTypeId in _options.PreferredGpuTypeIds)
        {
            if (string.IsNullOrWhiteSpace(gpuTypeId))
            {
                continue;
            }

            try
            {
                var availability = await runPodClient.GetGpuAvailabilityAsync(gpuTypeId, ct).ConfigureAwait(false);
                var gpuType = availability.GpuTypes.FirstOrDefault();
                var stockStatus = gpuType?.LowestPrice?.StockStatus;

                if (!string.IsNullOrWhiteSpace(stockStatus) && AvailableStockStatuses.Contains(stockStatus))
                {
                    _logger.LogInformation(
                        "GPU {GpuTypeId} has stock status {StockStatus}.",
                        gpuTypeId,
                        stockStatus);
                    availableGpus.Add(gpuTypeId);
                }
                else
                {
                    _logger.LogDebug(
                        "GPU {GpuTypeId} unavailable (stock: {StockStatus}). Skipping.",
                        gpuTypeId,
                        stockStatus ?? "None");
                }
            }
            catch (DomainNotFoundException)
            {
                _logger.LogDebug("GPU type {GpuTypeId} not found in RunPod. Skipping.", gpuTypeId);
            }
        }

        if (availableGpus.Count == 0)
        {
            throw new ManagedPodResolutionException(ApplicationErrorMessages.RunPod.NO_GPU_AVAILABLE);
        }

        return availableGpus;
    }

    private IReadOnlyDictionary<string, string> BuildPodDeployEnvironment()
    {
        var environment = new Dictionary<string, string>(_options.PodEnvironment, StringComparer.OrdinalIgnoreCase);
        environment["MODEL_NAME"] = VllmModelIdHelper.GetPrimaryModelId(_options);
        return environment;
    }

    private static bool IsOptionalConfigEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.TrimStart().StartsWith('(');
}
