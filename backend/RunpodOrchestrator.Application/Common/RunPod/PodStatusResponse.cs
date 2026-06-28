namespace RunpodOrchestrator.Application.Common.RunPod;

public sealed record PodStatusResponse
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public PodRuntimeDto? Runtime { get; init; }
}
