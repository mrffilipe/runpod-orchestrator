namespace RunpodOrchestrator.Infrastructure.Configurations;

public static class InfrastructureErrorMessages
{
    public static class RunPod
    {
        public const string API_KEY_REQUIRED = "RunPod:ApiKey is required.";
        public const string PREFERRED_GPU_TYPES_REQUIRED = "RunPod:PreferredGpuTypeIds must contain at least one GPU type.";
        public const string POD_NAME_REQUIRED = "RunPod:PodName is required.";
        public const string POD_IMAGE_OR_TEMPLATE_REQUIRED =
            "RunPod:PodImageName or RunPod:PodTemplateId must be configured.";
        public const string POD_PORTS_REQUIRED = "RunPod:PodPorts is required.";
        public const string VOLUME_MOUNT_PATH_REQUIRED = "RunPod:VolumeMountPath is required.";
        public const string VOLUME_GB_INVALID = "RunPod:VolumeInGb must be greater than zero.";
        public const string CONTAINER_DISK_GB_INVALID = "RunPod:ContainerDiskInGb must be greater than zero.";
        public const string REQUEST_TIMEOUT_INVALID = "RunPod:RequestTimeoutSeconds must be greater than zero.";
        public const string GPU_COUNT_INVALID = "RunPod:GpuCount must be greater than zero.";
        public const string VLLM_PORT_INVALID = "RunPod:VllmPort must be greater than zero.";
        public const string VLLM_HEALTH_CHECK_PATH_REQUIRED = "RunPod:VllmHealthCheckPath is required.";
        public const string POD_POLLING_INTERVAL_INVALID = "RunPod:PodPollingIntervalSeconds must be greater than zero.";
        public const string POD_STARTUP_TIMEOUT_INVALID = "RunPod:PodStartupTimeoutSeconds must be greater than zero.";
        public const string VLLM_HEALTH_CHECK_RETRIES_INVALID = "RunPod:VllmHealthCheckRetries must be greater than zero.";
        public const string VLLM_HEALTH_CHECK_RETRY_DELAY_INVALID = "RunPod:VllmHealthCheckRetryDelaySeconds must be greater than zero.";
        public const string VLLM_HEALTH_CHECK_TIMEOUT_INVALID = "RunPod:VllmHealthCheckTimeoutSeconds must be greater than zero.";
        public const string VLLM_PROXY_TIMEOUT_INVALID = "RunPod:VllmProxyTimeoutSeconds must be greater than zero.";
        public const string VLLM_DEFAULT_MODEL_REQUIRED = "RunPod:VllmDefaultModel is required.";
        public const string POD_IDLE_TIMEOUT_INVALID = "RunPod:PodIdleTimeoutMinutes must be greater than zero.";
        public const string POD_IDLE_CHECK_INTERVAL_INVALID = "RunPod:PodIdleCheckIntervalSeconds must be greater than zero.";
        public const string POD_RESUME_MAX_RETRIES_INVALID = "RunPod:PodResumeMaxRetries must be greater than zero.";
        public const string POD_RESUME_RETRY_BASE_DELAY_INVALID = "RunPod:PodResumeRetryBaseDelaySeconds must be greater than zero.";
        public const string VLLM_READINESS_MODELS_PATH_REQUIRED = "RunPod:VllmReadinessModelsPath is required.";
        public const string VLLM_RUNTIME_HEALTH_TTL_INVALID = "RunPod:VllmRuntimeHealthTtlSeconds must be greater than zero.";
        public const string VLLM_RUNTIME_HEALTH_RETRIES_INVALID = "RunPod:VllmRuntimeHealthRetries must be greater than zero.";
        public const string VLLM_RUNTIME_HEALTH_RETRY_DELAY_INVALID = "RunPod:VllmRuntimeHealthRetryDelaySeconds must be greater than zero.";
        public const string POD_TERMINATE_CHECK_INTERVAL_INVALID = "RunPod:PodTerminateCheckIntervalMinutes must be greater than zero.";
    }
}
