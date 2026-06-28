using RunpodOrchestrator.Domain.Enums;

namespace RunpodOrchestrator.API.Models;

public sealed record PodEnsureReadyResponse
{
    public required string Endpoint { get; init; }
    public required PodOrchestratorState State { get; init; }
}
