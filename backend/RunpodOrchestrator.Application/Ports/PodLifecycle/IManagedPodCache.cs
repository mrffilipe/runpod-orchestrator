namespace RunpodOrchestrator.Application.Ports.PodLifecycle;

public interface IManagedPodCache
{
    string? CachedPodId { get; }

    void SetCachedPodId(string podId);
    void Invalidate();
}
