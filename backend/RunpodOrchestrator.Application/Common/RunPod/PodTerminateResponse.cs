namespace RunpodOrchestrator.Application.Common.RunPod;

public sealed record PodTerminateResponse
{
    public required string Id { get; init; }
}
