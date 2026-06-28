namespace RunpodOrchestrator.Application.Ports.PodLifecycle;

public enum VllmReadinessMode
{
    Startup,
    Runtime
}

public interface IVllmReadinessChecker
{
    Task CheckReadinessAsync(string baseUrl, VllmReadinessMode mode, CancellationToken ct = default);
}
