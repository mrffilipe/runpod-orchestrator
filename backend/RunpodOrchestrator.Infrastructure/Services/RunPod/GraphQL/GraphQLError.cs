using System.Text.Json.Serialization;

namespace RunpodOrchestrator.Infrastructure.Services.RunPod.GraphQL;

public sealed record GraphQLError
{
    [JsonPropertyName("message")]
    public string? Message { get; init; }
}
