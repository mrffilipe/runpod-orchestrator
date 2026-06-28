using System.Text.Json.Serialization;

namespace RunpodOrchestrator.Infrastructure.Services.RunPod.GraphQL;

public sealed record GraphQLRequest
{
    [JsonPropertyName("query")]
    public required string Query { get; init; }
}
