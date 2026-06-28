namespace RunpodOrchestrator.Application.Common.RunPod;

public sealed record PodDeployResponse
{
    public required string Id { get; init; }
    public string? DesiredStatus { get; init; }
    public string? GpuTypeId { get; init; }
}
