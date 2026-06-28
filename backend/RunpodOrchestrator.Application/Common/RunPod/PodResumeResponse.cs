namespace RunpodOrchestrator.Application.Common.RunPod;

public sealed record PodResumeResponse
{
    public required string Id { get; init; }
    public required string DesiredStatus { get; init; }
}
