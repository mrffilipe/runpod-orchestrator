namespace RunpodOrchestrator.Application.Common.RunPod;

public sealed record PodRuntimeDto
{
    public int? UptimeInSeconds { get; init; }
    public IReadOnlyList<PodPortDto>? Ports { get; init; }
}
