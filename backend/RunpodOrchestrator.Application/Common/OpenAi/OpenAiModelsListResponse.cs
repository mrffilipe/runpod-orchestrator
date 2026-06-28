using System.Text.Json.Serialization;

namespace RunpodOrchestrator.Application.Common.OpenAi;

public sealed record OpenAiModelsListResponse
{
    [JsonPropertyName("object")]
    public string ObjectType { get; init; } = "list";

    [JsonPropertyName("data")]
    public IReadOnlyList<OpenAiModelItem> Data { get; init; } = [];
}

public sealed record OpenAiModelItem
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("object")]
    public string ObjectType { get; init; } = "model";

    [JsonPropertyName("created")]
    public long Created { get; init; }

    [JsonPropertyName("owned_by")]
    public string OwnedBy { get; init; } = "runpod-orchestrator";
}
