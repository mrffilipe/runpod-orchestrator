namespace RunpodOrchestrator.Application.Common.RunPod;

public sealed record PodListResponse
{
    public required IReadOnlyList<PodSummaryResponse> Pods { get; init; }
}
