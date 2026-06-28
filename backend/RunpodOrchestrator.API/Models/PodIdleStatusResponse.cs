using RunpodOrchestrator.Domain.Enums;

namespace RunpodOrchestrator.API.Models;

public sealed record PodIdleStatusResponse
{
    public required PodOrchestratorState State { get; init; }
    public DateTime? LastRequestTime { get; init; }
    public required bool IsPodRunning { get; init; }
    public required int IdleTimeoutMinutes { get; init; }
    public required int IdleCheckIntervalSeconds { get; init; }
}
