namespace RunpodOrchestrator.Application.Common.RunPod;

public sealed record PodSummaryResponse
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? DesiredStatus { get; init; }
    public string? GpuTypeId { get; init; }
    public bool IsRunning { get; init; }
}
