namespace RunpodOrchestrator.API.Models;

public sealed record PodTerminateManagedResponse
{
    public required IReadOnlyList<string> TerminatedPodIds { get; init; }
}
