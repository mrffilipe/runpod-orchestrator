namespace RunpodOrchestrator.Domain.Exceptions;

public static class DomainErrorMessages
{
    public static class Pod
    {
        public const string POD_ID_REQUIRED = "Pod ID is required.";
    }

    public static class PodLifecycle
    {
        public const string STARTUP_TIMEOUT = "Pod startup timed out.";
        public const string VLLM_HEALTH_CHECK_FAILED = "vLLM health check failed after all retries.";
        public const string POD_FAULTED = "Pod is in a faulted state and requires manual intervention.";
        public const string POD_ENDPOINT_UNAVAILABLE = "Pod ID is not available for proxy endpoint.";
    }
}
