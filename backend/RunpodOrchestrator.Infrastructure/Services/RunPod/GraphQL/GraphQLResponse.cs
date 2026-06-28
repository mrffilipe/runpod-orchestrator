using System.Text.Json.Serialization;

namespace RunpodOrchestrator.Infrastructure.Services.RunPod.GraphQL;

public sealed record GraphQLResponse<TData>
{
    [JsonPropertyName("data")]
    public TData? Data { get; init; }

    [JsonPropertyName("errors")]
    public IReadOnlyList<GraphQLError>? Errors { get; init; }
}
