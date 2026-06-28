namespace RunpodOrchestrator.Application.Ports.PodLifecycle;

public interface IManagedPodCleanupService
{
    Task<IReadOnlyList<string>> ListManagedPodIdsAsync(CancellationToken ct = default);
    Task TerminateAllManagedPodsAsync(CancellationToken ct = default);
}
