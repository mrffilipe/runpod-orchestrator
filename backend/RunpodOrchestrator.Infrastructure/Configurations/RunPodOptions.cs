namespace RunpodOrchestrator.Infrastructure.Configurations;

public sealed record RunPodOptions
{
    public const string SECTION = "RunPod";

    public required string ApiKey { get; init; }
    public IReadOnlyList<string> PreferredGpuTypeIds { get; init; } = ["NVIDIA GeForce RTX 4090"];
    public string PodName { get; init; } = "runpod-orchestrator";
    public string? PodTemplateId { get; init; }
    public string PodImageName { get; init; } = "vllm/vllm-openai:latest";
    public string PodPorts { get; init; } = "8000/http";
    public string VolumeMountPath { get; init; } = "/workspace";
    public int VolumeInGb { get; init; } = 40;
    public int ContainerDiskInGb { get; init; } = 40;
    public string? NetworkVolumeId { get; init; }
    public string BaseUrl { get; init; } = "https://api.runpod.io/graphql";
    public int RequestTimeoutSeconds { get; init; } = 30;
    public int GpuCount { get; init; } = 1;
    public int VllmPort { get; init; } = 8000;
    public string VllmHealthCheckPath { get; init; } = "/health";
    public int PodPollingIntervalSeconds { get; init; } = 5;
    public int PodStartupTimeoutSeconds { get; init; } = 600;
    public int VllmHealthCheckRetries { get; init; } = 12;
    public int VllmHealthCheckRetryDelaySeconds { get; init; } = 5;
    public int VllmHealthCheckTimeoutSeconds { get; init; } = 10;
    public string VllmReadinessModelsPath { get; init; } = "/v1/models";
    public bool VllmReadinessRequireModel { get; init; } = true;
    public int VllmRuntimeHealthTtlSeconds { get; init; } = 300;
    public int VllmRuntimeHealthRetries { get; init; } = 3;
    public int VllmRuntimeHealthRetryDelaySeconds { get; init; } = 2;
    public int VllmProxyTimeoutSeconds { get; init; } = 600;
    public bool VllmProxyRetryOnFailure { get; init; } = true;
    public int PodIdleTimeoutMinutes { get; init; } = 15;
    public int PodIdleCheckIntervalSeconds { get; init; } = 60;
    public int PodTerminateCheckIntervalMinutes { get; init; } = 60;
    public bool PodTerminateAfterIdleEnabled { get; init; } = true;
    public int PodResumeMaxRetries { get; init; } = 3;
    public int PodResumeRetryBaseDelaySeconds { get; init; } = 2;
    public bool PodHealthRecoveryEnabled { get; init; } = true;
    public string VllmDefaultModel { get; init; } = "Qwen/Qwen3-8B";
    public IReadOnlyDictionary<string, string> PodEnvironment { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
