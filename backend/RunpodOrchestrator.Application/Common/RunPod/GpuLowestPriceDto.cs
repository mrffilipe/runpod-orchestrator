namespace RunpodOrchestrator.Application.Common.RunPod;

public sealed record GpuLowestPriceDto
{
    public string? StockStatus { get; init; }
    public IReadOnlyList<int>? AvailableGpuCounts { get; init; }
}
