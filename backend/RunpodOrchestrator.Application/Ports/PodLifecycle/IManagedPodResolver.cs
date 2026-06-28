namespace RunpodOrchestrator.Application.Ports.PodLifecycle;

public interface IManagedPodResolver
{
    Task<string> GetManagedPodIdAsync(CancellationToken ct = default);

    void InvalidateCache();
}
