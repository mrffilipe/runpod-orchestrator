using Microsoft.Extensions.Options;

namespace RunpodOrchestrator.Infrastructure.Configurations;

public sealed class RunPodOptionsValidator : IValidateOptions<RunPodOptions>
{
    public ValidateOptionsResult Validate(string? name, RunPodOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            errors.Add(InfrastructureErrorMessages.RunPod.API_KEY_REQUIRED);
        }

        if (options.PreferredGpuTypeIds is null ||
            options.PreferredGpuTypeIds.Count == 0 ||
            options.PreferredGpuTypeIds.All(string.IsNullOrWhiteSpace))
        {
            errors.Add(InfrastructureErrorMessages.RunPod.PREFERRED_GPU_TYPES_REQUIRED);
        }

        if (string.IsNullOrWhiteSpace(options.PodName))
        {
            errors.Add(InfrastructureErrorMessages.RunPod.POD_NAME_REQUIRED);
        }

        var hasTemplate = !string.IsNullOrWhiteSpace(options.PodTemplateId);

        if (!hasTemplate && string.IsNullOrWhiteSpace(options.PodImageName))
        {
            errors.Add(InfrastructureErrorMessages.RunPod.POD_IMAGE_OR_TEMPLATE_REQUIRED);
        }

        if (!hasTemplate)
        {
            if (string.IsNullOrWhiteSpace(options.PodPorts))
            {
                errors.Add(InfrastructureErrorMessages.RunPod.POD_PORTS_REQUIRED);
            }

            if (string.IsNullOrWhiteSpace(options.VolumeMountPath))
            {
                errors.Add(InfrastructureErrorMessages.RunPod.VOLUME_MOUNT_PATH_REQUIRED);
            }

            if (options.VolumeInGb <= 0)
            {
                errors.Add(InfrastructureErrorMessages.RunPod.VOLUME_GB_INVALID);
            }

            if (options.ContainerDiskInGb <= 0)
            {
                errors.Add(InfrastructureErrorMessages.RunPod.CONTAINER_DISK_GB_INVALID);
            }
        }

        if (options.RequestTimeoutSeconds <= 0)
        {
            errors.Add(InfrastructureErrorMessages.RunPod.REQUEST_TIMEOUT_INVALID);
        }

        if (options.GpuCount <= 0)
        {
            errors.Add(InfrastructureErrorMessages.RunPod.GPU_COUNT_INVALID);
        }

        if (options.VllmPort <= 0)
        {
            errors.Add(InfrastructureErrorMessages.RunPod.VLLM_PORT_INVALID);
        }

        if (string.IsNullOrWhiteSpace(options.VllmHealthCheckPath))
        {
            errors.Add(InfrastructureErrorMessages.RunPod.VLLM_HEALTH_CHECK_PATH_REQUIRED);
        }

        if (options.PodPollingIntervalSeconds <= 0)
        {
            errors.Add(InfrastructureErrorMessages.RunPod.POD_POLLING_INTERVAL_INVALID);
        }

        if (options.PodStartupTimeoutSeconds <= 0)
        {
            errors.Add(InfrastructureErrorMessages.RunPod.POD_STARTUP_TIMEOUT_INVALID);
        }

        if (options.VllmHealthCheckRetries <= 0)
        {
            errors.Add(InfrastructureErrorMessages.RunPod.VLLM_HEALTH_CHECK_RETRIES_INVALID);
        }

        if (options.VllmHealthCheckRetryDelaySeconds <= 0)
        {
            errors.Add(InfrastructureErrorMessages.RunPod.VLLM_HEALTH_CHECK_RETRY_DELAY_INVALID);
        }

        if (options.VllmHealthCheckTimeoutSeconds <= 0)
        {
            errors.Add(InfrastructureErrorMessages.RunPod.VLLM_HEALTH_CHECK_TIMEOUT_INVALID);
        }

        if (options.VllmProxyTimeoutSeconds <= 0)
        {
            errors.Add(InfrastructureErrorMessages.RunPod.VLLM_PROXY_TIMEOUT_INVALID);
        }

        if (string.IsNullOrWhiteSpace(options.VllmDefaultModel))
        {
            errors.Add(InfrastructureErrorMessages.RunPod.VLLM_DEFAULT_MODEL_REQUIRED);
        }

        if (options.PodIdleTimeoutMinutes <= 0)
        {
            errors.Add(InfrastructureErrorMessages.RunPod.POD_IDLE_TIMEOUT_INVALID);
        }

        if (options.PodIdleCheckIntervalSeconds <= 0)
        {
            errors.Add(InfrastructureErrorMessages.RunPod.POD_IDLE_CHECK_INTERVAL_INVALID);
        }

        if (options.PodResumeMaxRetries <= 0)
        {
            errors.Add(InfrastructureErrorMessages.RunPod.POD_RESUME_MAX_RETRIES_INVALID);
        }

        if (options.PodResumeRetryBaseDelaySeconds <= 0)
        {
            errors.Add(InfrastructureErrorMessages.RunPod.POD_RESUME_RETRY_BASE_DELAY_INVALID);
        }

        if (string.IsNullOrWhiteSpace(options.VllmReadinessModelsPath))
        {
            errors.Add(InfrastructureErrorMessages.RunPod.VLLM_READINESS_MODELS_PATH_REQUIRED);
        }

        if (options.VllmRuntimeHealthTtlSeconds <= 0)
        {
            errors.Add(InfrastructureErrorMessages.RunPod.VLLM_RUNTIME_HEALTH_TTL_INVALID);
        }

        if (options.VllmRuntimeHealthRetries <= 0)
        {
            errors.Add(InfrastructureErrorMessages.RunPod.VLLM_RUNTIME_HEALTH_RETRIES_INVALID);
        }

        if (options.VllmRuntimeHealthRetryDelaySeconds <= 0)
        {
            errors.Add(InfrastructureErrorMessages.RunPod.VLLM_RUNTIME_HEALTH_RETRY_DELAY_INVALID);
        }

        if (options.PodTerminateCheckIntervalMinutes <= 0)
        {
            errors.Add(InfrastructureErrorMessages.RunPod.POD_TERMINATE_CHECK_INTERVAL_INVALID);
        }

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
