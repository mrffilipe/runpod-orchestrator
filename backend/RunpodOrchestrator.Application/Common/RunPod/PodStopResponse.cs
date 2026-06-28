namespace RunpodOrchestrator.Application.Common.RunPod;

public sealed record PodStopResponse
{
    public required string Id { get; init; }
    public required string DesiredStatus { get; init; }
}
