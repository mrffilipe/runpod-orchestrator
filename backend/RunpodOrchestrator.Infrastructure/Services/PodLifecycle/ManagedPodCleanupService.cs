using RunpodOrchestrator.Application.Exceptions;
using RunpodOrchestrator.Application.Ports.PodLifecycle;
using RunpodOrchestrator.Application.Ports.RunPod;
using RunpodOrchestrator.Domain.Exceptions;
using RunpodOrchestrator.Infrastructure.Configurations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RunpodOrchestrator.Infrastructure.Services.PodLifecycle;

public sealed class ManagedPodCleanupService : IManagedPodCleanupService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IManagedPodCache _managedPodCache;
    private readonly RunPodOptions _options;
    private readonly ILogger<ManagedPodCleanupService> _logger;

    public ManagedPodCleanupService(
        IServiceScopeFactory scopeFactory,
        IManagedPodCache managedPodCache,
        IOptions<RunPodOptions> options,
        ILogger<ManagedPodCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _managedPodCache = managedPodCache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> ListManagedPodIdsAsync(CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var runPodClient = scope.ServiceProvider.GetRequiredService<IRunPodApiClient>();

        var podList = await runPodClient.ListPodsAsync(ct).ConfigureAwait(false);
        return podList.Pods
            .Where(pod => ManagedPodFilter.IsManaged(pod, _options))
            .Select(p => p.Id)
            .ToList();
    }

    public async Task TerminateAllManagedPodsAsync(CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var runPodClient = scope.ServiceProvider.GetRequiredService<IRunPodApiClient>();

        var podList = await runPodClient.ListPodsAsync(ct).ConfigureAwait(false);
        var managedPods = podList.Pods.Where(pod => ManagedPodFilter.IsManaged(pod, _options)).ToList();

        if (managedPods.Count == 0)
        {
            _logger.LogInformation("No managed pods named {PodName} found to terminate.", _options.PodName);
            _managedPodCache.Invalidate();
            return;
        }

        _logger.LogInformation(
            "Terminating {Count} managed pod(s) named {PodName}.",
            managedPods.Count,
            _options.PodName);

        foreach (var pod in managedPods)
        {
            try
            {
                await runPodClient.TerminatePodAsync(pod.Id, ct).ConfigureAwait(false);
                _logger.LogInformation("Terminated managed pod {PodId} ({PodName}).", pod.Id, pod.Name);
            }
            catch (DomainNotFoundException ex)
            {
                _logger.LogWarning(ex, "Managed pod {PodId} not found during terminate.", pod.Id);
            }
            catch (RunPodApiException ex)
            {
                _logger.LogError(ex, "Failed to terminate managed pod {PodId}. Continuing with remaining pods.", pod.Id);
            }
        }

        _managedPodCache.Invalidate();
    }
}
