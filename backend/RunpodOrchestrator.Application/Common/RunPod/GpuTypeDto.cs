namespace RunpodOrchestrator.Application.Common.RunPod;

public sealed record GpuTypeDto
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public GpuLowestPriceDto? LowestPrice { get; init; }
}
