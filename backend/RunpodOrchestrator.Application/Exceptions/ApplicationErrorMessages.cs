namespace RunpodOrchestrator.Application.Exceptions;

public static class ApplicationErrorMessages
{
    public static class RunPod
    {
        public const string GRAPHQL_ERRORS = "RunPod GraphQL API returned errors.";
        public const string EMPTY_RESPONSE = "RunPod GraphQL API returned an empty response.";
        public const string HTTP_REQUEST_FAILED = "RunPod GraphQL API request failed.";
        public const string POD_NOT_FOUND = "Pod was not found in RunPod.";
        public const string GPU_TYPE_NOT_FOUND = "GPU type was not found in RunPod.";
        public const string NO_GPU_AVAILABLE = "No preferred GPU type is currently available in RunPod.";
        public const string POD_DEPLOY_FAILED = "Failed to deploy a new RunPod pod.";
    }

    public static class ManagedPod
    {
        public const string RESOLUTION_FAILED = "Failed to resolve or create the managed RunPod pod.";
    }

    public static class VllmProxy
    {
        public const string REQUEST_FAILED = "vLLM proxy request failed.";
    }
}
