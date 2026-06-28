namespace RunpodOrchestrator.Application.Common.RunPod;

public sealed record GpuAvailabilityResponse
{
    public required IReadOnlyList<GpuTypeDto> GpuTypes { get; init; }
}
