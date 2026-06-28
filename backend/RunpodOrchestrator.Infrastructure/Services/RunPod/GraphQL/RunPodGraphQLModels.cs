using System.Text.Json.Serialization;

namespace RunpodOrchestrator.Infrastructure.Services.RunPod.GraphQL;

internal sealed record PodStatusData
{
    [JsonPropertyName("pod")]
    public PodGraphQL? Pod { get; init; }
}

internal sealed record PodResumeData
{
    [JsonPropertyName("podResume")]
    public PodMutationGraphQL? PodResume { get; init; }
}

internal sealed record PodStopData
{
    [JsonPropertyName("podStop")]
    public PodMutationGraphQL? PodStop { get; init; }
}

internal sealed record GpuTypesData
{
    [JsonPropertyName("gpuTypes")]
    public IReadOnlyList<GpuTypeGraphQL>? GpuTypes { get; init; }
}

internal sealed record MyselfPodsData
{
    [JsonPropertyName("myself")]
    public MyselfGraphQL? Myself { get; init; }
}

internal sealed record MyselfGraphQL
{
    [JsonPropertyName("pods")]
    public IReadOnlyList<PodListItemGraphQL>? Pods { get; init; }
}

internal sealed record PodDeployData
{
    [JsonPropertyName("podFindAndDeployOnDemand")]
    public PodDeployResultGraphQL? PodFindAndDeployOnDemand { get; init; }
}

internal sealed record PodListItemGraphQL
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("desiredStatus")]
    public string? DesiredStatus { get; init; }

    [JsonPropertyName("runtime")]
    public PodRuntimeGraphQL? Runtime { get; init; }

    [JsonPropertyName("machine")]
    public PodMachineGraphQL? Machine { get; init; }
}

internal sealed record PodMachineGraphQL
{
    [JsonPropertyName("gpuDisplayName")]
    public string? GpuDisplayName { get; init; }
}

internal sealed record PodDeployResultGraphQL
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("desiredStatus")]
    public string? DesiredStatus { get; init; }

    [JsonPropertyName("machine")]
    public PodMachineGraphQL? Machine { get; init; }
}

internal sealed record PodGraphQL
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("runtime")]
    public PodRuntimeGraphQL? Runtime { get; init; }
}

internal sealed record PodMutationGraphQL
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("desiredStatus")]
    public string? DesiredStatus { get; init; }
}

internal sealed record PodRuntimeGraphQL
{
    [JsonPropertyName("uptimeInSeconds")]
    public int? UptimeInSeconds { get; init; }

    [JsonPropertyName("ports")]
    public IReadOnlyList<PodPortGraphQL>? Ports { get; init; }
}

internal sealed record PodPortGraphQL
{
    [JsonPropertyName("ip")]
    public string? Ip { get; init; }

    [JsonPropertyName("isIpPublic")]
    public bool? IsPublic { get; init; }

    [JsonPropertyName("privatePort")]
    public int? PrivatePort { get; init; }

    [JsonPropertyName("publicPort")]
    public int? PublicPort { get; init; }
}

internal sealed record GpuTypeGraphQL
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("lowestPrice")]
    public GpuLowestPriceGraphQL? LowestPrice { get; init; }
}

internal sealed record GpuLowestPriceGraphQL
{
    [JsonPropertyName("stockStatus")]
    public string? StockStatus { get; init; }

    [JsonPropertyName("availableGpuCounts")]
    public IReadOnlyList<int>? AvailableGpuCounts { get; init; }
}
