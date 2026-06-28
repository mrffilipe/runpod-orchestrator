using RunpodOrchestrator.Domain.Enums;

namespace RunpodOrchestrator.Application.Ports.PodLifecycle;

public interface IPodManagerService
{
    PodOrchestratorState CurrentState { get; }

    Task<string> EnsurePodIsReadyAsync(CancellationToken ct = default);
    void UpdateLastRequestTime();
    DateTime? GetLastRequestTime();
    bool IsPodRunning();
    void SetPodToStopped();
    void InvalidateReadyState();
}
