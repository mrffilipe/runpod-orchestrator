using RunpodOrchestrator.Application.Exceptions;
using RunpodOrchestrator.Application.Ports.PodLifecycle;
using RunpodOrchestrator.Application.Ports.RunPod;
using RunpodOrchestrator.Domain.Exceptions;
using RunpodOrchestrator.Infrastructure.Configurations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RunpodOrchestrator.Infrastructure.Services.PodLifecycle;

public sealed class IdleMonitorService : BackgroundService
{
    private readonly IPodManagerService _podManagerService;
    private readonly IManagedPodResolver _managedPodResolver;
    private readonly IManagedPodCleanupService _managedPodCleanupService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RunPodOptions _options;
    private readonly ILogger<IdleMonitorService> _logger;
    private DateTime _lastTerminateCheckUtc = DateTime.UtcNow;

    public IdleMonitorService(
        IPodManagerService podManagerService,
        IManagedPodResolver managedPodResolver,
        IManagedPodCleanupService managedPodCleanupService,
        IServiceScopeFactory scopeFactory,
        IOptions<RunPodOptions> options,
        ILogger<IdleMonitorService> logger)
    {
        _podManagerService = podManagerService;
        _managedPodResolver = managedPodResolver;
        _managedPodCleanupService = managedPodCleanupService;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Idle monitor started. Timeout: {TimeoutMinutes} min, check interval: {IntervalSeconds}s, terminate check: {TerminateCheckMinutes} min.",
            _options.PodIdleTimeoutMinutes,
            _options.PodIdleCheckIntervalSeconds,
            _options.PodTerminateCheckIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.PodIdleCheckIntervalSeconds), stoppingToken)
                    .ConfigureAwait(false);

                await CheckIdleAndStopPodAsync(stoppingToken).ConfigureAwait(false);
                await CheckIdleAndTerminatePodsAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in idle monitor loop.");
            }
        }

        _logger.LogInformation("Idle monitor stopped.");
    }

    private async Task CheckIdleAndStopPodAsync(CancellationToken stoppingToken)
    {
        if (!_podManagerService.IsPodRunning())
        {
            _logger.LogDebug("Idle check skipped: pod is not in ready state.");
            return;
        }

        var lastRequestTime = _podManagerService.GetLastRequestTime();
        if (lastRequestTime is null)
        {
            return;
        }

        var idleDuration = DateTime.UtcNow - lastRequestTime.Value;
        var idleTimeout = TimeSpan.FromMinutes(_options.PodIdleTimeoutMinutes);

        if (idleDuration <= idleTimeout)
        {
            return;
        }

        string podId;
        try
        {
            podId = await _managedPodResolver.GetManagedPodIdAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (ManagedPodResolutionException ex)
        {
            _logger.LogError(ex, "Failed to resolve managed pod for idle stop.");
            return;
        }

        _logger.LogInformation(
            "Pod {PodId} idle for {IdleMinutes:F1} minutes (timeout: {TimeoutMinutes} min). Sending Stop command.",
            podId,
            idleDuration.TotalMinutes,
            _options.PodIdleTimeoutMinutes);

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var runPodClient = scope.ServiceProvider.GetRequiredService<IRunPodApiClient>();

            await runPodClient.StopPodAsync(podId, stoppingToken).ConfigureAwait(false);
            _podManagerService.SetPodToStopped();

            _logger.LogInformation("Pod {PodId} stopped due to inactivity.", podId);
        }
        catch (DomainNotFoundException ex)
        {
            _managedPodResolver.InvalidateCache();
            _logger.LogError(ex, "Managed pod {PodId} not found during idle stop. Cache invalidated.", podId);
        }
        catch (RunPodApiException ex)
        {
            _logger.LogError(
                ex,
                "Failed to stop pod {PodId} due to inactivity. Will retry on next check.",
                podId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error stopping pod {PodId} due to inactivity. Will retry on next check.",
                podId);
        }
    }

    private async Task CheckIdleAndTerminatePodsAsync(CancellationToken stoppingToken)
    {
        if (!_options.PodTerminateAfterIdleEnabled)
        {
            return;
        }

        var terminateCheckInterval = TimeSpan.FromMinutes(_options.PodTerminateCheckIntervalMinutes);
        if (DateTime.UtcNow - _lastTerminateCheckUtc < terminateCheckInterval)
        {
            return;
        }

        _lastTerminateCheckUtc = DateTime.UtcNow;

        var lastRequestTime = _podManagerService.GetLastRequestTime();
        if (lastRequestTime is null)
        {
            return;
        }

        var idleDuration = DateTime.UtcNow - lastRequestTime.Value;
        var idleTimeout = TimeSpan.FromMinutes(_options.PodIdleTimeoutMinutes);

        if (idleDuration <= idleTimeout)
        {
            _logger.LogDebug("Terminate check skipped: idle duration has not exceeded timeout yet.");
            return;
        }

        _logger.LogInformation(
            "Post-idle terminate check triggered. Last request was {IdleMinutes:F1} minutes ago (timeout: {TimeoutMinutes} min).",
            idleDuration.TotalMinutes,
            _options.PodIdleTimeoutMinutes);

        try
        {
            await _managedPodCleanupService.TerminateAllManagedPodsAsync(ct: stoppingToken).ConfigureAwait(false);
            _podManagerService.SetPodToStopped();
            _logger.LogInformation("Managed pods terminated due to extended inactivity.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to terminate managed pods during post-idle cleanup. Will retry on next check.");
        }
    }
}
