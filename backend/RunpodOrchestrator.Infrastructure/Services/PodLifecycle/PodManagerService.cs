using RunpodOrchestrator.Application.Common.RunPod;
using RunpodOrchestrator.Application.Exceptions;
using RunpodOrchestrator.Application.Ports.PodLifecycle;
using RunpodOrchestrator.Application.Ports.RunPod;
using RunpodOrchestrator.Domain.Enums;
using RunpodOrchestrator.Domain.Exceptions;
using RunpodOrchestrator.Infrastructure.Configurations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RunpodOrchestrator.Infrastructure.Services.PodLifecycle;

public sealed class PodManagerService : IPodManagerService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IVllmReadinessChecker _readinessChecker;
    private readonly IManagedPodResolver _managedPodResolver;
    private readonly RunPodOptions _options;
    private readonly ILogger<PodManagerService> _logger;
    private readonly SemaphoreSlim _podStartSemaphore = new(1, 1);

    private TaskCompletionSource<string>? _podReadyTcs;
    private string? _podEndpoint;
    private string? _activePodId;
    private PodOrchestratorState _currentState = PodOrchestratorState.Stopped;
    private long _lastRequestTimeTicks;
    private DateTime _lastHealthCheckUtc = DateTime.MinValue;

    public PodManagerService(
        IServiceScopeFactory scopeFactory,
        IVllmReadinessChecker readinessChecker,
        IManagedPodResolver managedPodResolver,
        IOptions<RunPodOptions> options,
        ILogger<PodManagerService> logger)
    {
        _scopeFactory = scopeFactory;
        _readinessChecker = readinessChecker;
        _managedPodResolver = managedPodResolver;
        _options = options.Value;
        _logger = logger;
    }

    public PodOrchestratorState CurrentState => _currentState;

    public void UpdateLastRequestTime() =>
        Interlocked.Exchange(ref _lastRequestTimeTicks, DateTime.UtcNow.Ticks);

    public DateTime? GetLastRequestTime()
    {
        var ticks = Interlocked.Read(ref _lastRequestTimeTicks);
        return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
    }

    public bool IsPodRunning() => _currentState == PodOrchestratorState.Ready;

    public void SetPodToStopped()
    {
        ResetStartupState();
        _logger.LogInformation("Managed pod {PodId} marked as stopped internally.", _activePodId ?? "unknown");
    }

    public void InvalidateReadyState()
    {
        _podReadyTcs = null;
        _podEndpoint = null;
        _currentState = PodOrchestratorState.Stopped;
        _lastHealthCheckUtc = DateTime.MinValue;
        _logger.LogWarning("Managed pod {PodId} ready state invalidated.", _activePodId ?? "unknown");
    }

    public async Task<string> EnsurePodIsReadyAsync(CancellationToken ct = default)
    {
        if (_currentState == PodOrchestratorState.Faulted)
        {
            throw new PodFaultedException(DomainErrorMessages.PodLifecycle.POD_FAULTED);
        }

        if (_currentState == PodOrchestratorState.Ready && !string.IsNullOrWhiteSpace(_podEndpoint))
        {
            if (!IsHealthCheckStale())
            {
                _logger.LogDebug("Pod is already ready at {Endpoint}.", _podEndpoint);
                return _podEndpoint;
            }

            try
            {
                await _readinessChecker
                    .CheckReadinessAsync(_podEndpoint, VllmReadinessMode.Runtime, ct)
                    .ConfigureAwait(false);
                _lastHealthCheckUtc = DateTime.UtcNow;
                _logger.LogDebug("Pod runtime health revalidated at {Endpoint}.", _podEndpoint);
                return _podEndpoint;
            }
            catch (DomainBusinessRuleException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Runtime health revalidation failed at {Endpoint}. Restarting ensure flow.",
                    _podEndpoint);
                InvalidateReadyState();
            }
        }

        await _podStartSemaphore.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            if (_currentState == PodOrchestratorState.Faulted)
            {
                throw new PodFaultedException(DomainErrorMessages.PodLifecycle.POD_FAULTED);
            }

            if (_currentState == PodOrchestratorState.Ready && !string.IsNullOrWhiteSpace(_podEndpoint))
            {
                _logger.LogDebug("Pod became ready while waiting for semaphore at {Endpoint}.", _podEndpoint);
                return _podEndpoint;
            }

            if (_podReadyTcs is not null && !_podReadyTcs.Task.IsCompleted)
            {
                _logger.LogDebug("Awaiting in-progress pod startup.");
                return await _podReadyTcs.Task.WaitAsync(ct).ConfigureAwait(false);
            }

            _podReadyTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _currentState = PodOrchestratorState.Starting;

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var runPodClient = scope.ServiceProvider.GetRequiredService<IRunPodApiClient>();

                var podId = await _managedPodResolver.GetManagedPodIdAsync(ct).ConfigureAwait(false);
                _activePodId = podId;

                var status = await GetPodStatusAsync(runPodClient, podId, ct).ConfigureAwait(false);
                string endpoint;

                if (status.Runtime is null)
                {
                    _logger.LogInformation(
                        "Pod {PodId} is stopped. Resuming with {GpuCount} GPU(s).",
                        podId,
                        _options.GpuCount);

                    await ResumePodWithRetryAsync(runPodClient, podId, ct).ConfigureAwait(false);
                    endpoint = await PollForPodReadinessAsync(runPodClient, podId, ct).ConfigureAwait(false);
                }
                else
                {
                    endpoint = BuildVllmBaseUrl(status);
                    endpoint = await EnsureVllmHealthyAsync(runPodClient, podId, endpoint, ct)
                        .ConfigureAwait(false);
                }

                _podEndpoint = endpoint;
                _currentState = PodOrchestratorState.Ready;
                _lastHealthCheckUtc = DateTime.UtcNow;
                _podReadyTcs.SetResult(endpoint);

                _logger.LogInformation("Pod {PodId} is ready at {Endpoint}.", podId, endpoint);
                return endpoint;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pod {PodId} startup failed.", _activePodId ?? "unknown");
                _podReadyTcs.TrySetException(ex);

                if (_currentState != PodOrchestratorState.Faulted)
                {
                    ResetStartupState();
                }

                throw;
            }
        }
        finally
        {
            _podStartSemaphore.Release();
        }
    }

    internal void ResetStartupState()
    {
        _podReadyTcs = null;
        _podEndpoint = null;
        _currentState = PodOrchestratorState.Stopped;
        _lastHealthCheckUtc = DateTime.MinValue;
    }

    private bool IsHealthCheckStale() =>
        _lastHealthCheckUtc == DateTime.MinValue
        || DateTime.UtcNow - _lastHealthCheckUtc > TimeSpan.FromSeconds(_options.VllmRuntimeHealthTtlSeconds);

    private async Task<PodStatusResponse> GetPodStatusAsync(
        IRunPodApiClient runPodClient,
        string podId,
        CancellationToken ct)
    {
        try
        {
            return await runPodClient.GetPodStatusAsync(podId, ct).ConfigureAwait(false);
        }
        catch (DomainNotFoundException)
        {
            _managedPodResolver.InvalidateCache();
            throw;
        }
    }

    private async Task ResumePodWithRetryAsync(IRunPodApiClient runPodClient, string podId, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= _options.PodResumeMaxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await runPodClient.ResumePodAsync(podId, _options.GpuCount, ct).ConfigureAwait(false);
                _logger.LogInformation(
                    "Pod {PodId} resume succeeded on attempt {Attempt}/{MaxAttempts}.",
                    podId,
                    attempt,
                    _options.PodResumeMaxRetries);
                return;
            }
            catch (RunPodApiException ex) when (attempt < _options.PodResumeMaxRetries)
            {
                var delaySeconds = CalculateRetryDelaySeconds(attempt, _options.PodResumeRetryBaseDelaySeconds);
                _logger.LogWarning(
                    ex,
                    "Pod {PodId} resume failed on attempt {Attempt}/{MaxAttempts}. Retrying in {DelaySeconds}s.",
                    podId,
                    attempt,
                    _options.PodResumeMaxRetries,
                    delaySeconds);

                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct).ConfigureAwait(false);
            }
            catch (RunPodApiException ex)
            {
                _logger.LogError(
                    ex,
                    "Pod {PodId} resume failed after {MaxAttempts} attempts.",
                    podId,
                    _options.PodResumeMaxRetries);
                throw;
            }
        }
    }

    private static int CalculateRetryDelaySeconds(int attempt, int baseDelaySeconds) =>
        (int)Math.Min(60, Math.Pow(2, attempt - 1) * baseDelaySeconds);

    private async Task<string> PollForPodReadinessAsync(
        IRunPodApiClient runPodClient,
        string podId,
        CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(_options.PodStartupTimeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            var status = await GetPodStatusAsync(runPodClient, podId, ct).ConfigureAwait(false);

            if (status.Runtime is not null)
            {
                var endpoint = BuildVllmBaseUrl(status);
                _logger.LogInformation(
                    "Pod {PodId} runtime detected. Checking vLLM readiness at {Endpoint}.",
                    podId,
                    endpoint);

                try
                {
                    await _readinessChecker
                        .CheckReadinessAsync(endpoint, VllmReadinessMode.Startup, ct)
                        .ConfigureAwait(false);
                    return endpoint;
                }
                catch (DomainBusinessRuleException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Pod {PodId} vLLM not ready yet at {Endpoint}. Polling again in {IntervalSeconds}s.",
                        podId,
                        endpoint,
                        _options.PodPollingIntervalSeconds);
                }
            }
            else
            {
                _logger.LogInformation(
                    "Pod {PodId} not ready yet. Polling again in {IntervalSeconds}s.",
                    podId,
                    _options.PodPollingIntervalSeconds);
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.PodPollingIntervalSeconds), ct).ConfigureAwait(false);
        }

        _logger.LogWarning(
            "Pod {PodId} startup timed out after {TimeoutSeconds}s.",
            podId,
            _options.PodStartupTimeoutSeconds);

        throw new PodStartupTimeoutException(DomainErrorMessages.PodLifecycle.STARTUP_TIMEOUT);
    }

    private async Task<string> EnsureVllmHealthyAsync(
        IRunPodApiClient runPodClient,
        string podId,
        string endpoint,
        CancellationToken ct)
    {
        try
        {
            await _readinessChecker
                .CheckReadinessAsync(endpoint, VllmReadinessMode.Startup, ct)
                .ConfigureAwait(false);
            return endpoint;
        }
        catch (DomainBusinessRuleException)
        {
            if (_options.PodHealthRecoveryEnabled)
            {
                return await AttemptHealthRecoveryAsync(runPodClient, podId, ct).ConfigureAwait(false);
            }

            MarkPodFaulted();
            throw new PodFaultedException(DomainErrorMessages.PodLifecycle.POD_FAULTED);
        }
    }

    private async Task<string> AttemptHealthRecoveryAsync(
        IRunPodApiClient runPodClient,
        string podId,
        CancellationToken ct)
    {
        _logger.LogWarning(
            "vLLM readiness check failed for pod {PodId}. Attempting stop + resume recovery.",
            podId);

        try
        {
            await runPodClient.StopPodAsync(podId, ct).ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromSeconds(_options.PodPollingIntervalSeconds), ct).ConfigureAwait(false);
            await ResumePodWithRetryAsync(runPodClient, podId, ct).ConfigureAwait(false);
            return await PollForPodReadinessAsync(runPodClient, podId, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pod {PodId} health recovery failed.", podId);
            MarkPodFaulted();
            throw new PodFaultedException(DomainErrorMessages.PodLifecycle.POD_FAULTED, ex);
        }
    }

    private void MarkPodFaulted()
    {
        _podReadyTcs?.TrySetException(new PodFaultedException(DomainErrorMessages.PodLifecycle.POD_FAULTED));
        _podEndpoint = null;
        _currentState = PodOrchestratorState.Faulted;
        _lastHealthCheckUtc = DateTime.MinValue;
        _logger.LogError("Pod {PodId} marked as faulted.", _activePodId ?? "unknown");
    }

    private string BuildVllmBaseUrl(PodStatusResponse status)
    {
        if (string.IsNullOrWhiteSpace(status.Id))
        {
            throw new DomainBusinessRuleException(DomainErrorMessages.PodLifecycle.POD_ENDPOINT_UNAVAILABLE);
        }

        if (_options.VllmPort <= 0)
        {
            throw new DomainBusinessRuleException(DomainErrorMessages.PodLifecycle.POD_ENDPOINT_UNAVAILABLE);
        }

        return $"https://{status.Id}-{_options.VllmPort}.proxy.runpod.net";
    }
}
