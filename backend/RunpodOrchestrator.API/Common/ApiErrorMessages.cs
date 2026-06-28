namespace RunpodOrchestrator.API.Common;

public static class ApiErrorMessages
{
    public const string DOMAIN_VALIDATION_TITLE = "Domain Validation Error";
    public const string DOMAIN_BUSINESS_RULE_TITLE = "Domain Business Rule Error";
    public const string NOT_FOUND_TITLE = "Not Found";
    public const string RUNPOD_API_TITLE = "RunPod API Error";
    public const string VLLM_PROXY_TITLE = "vLLM Proxy Error";
    public const string POD_FAULTED_TITLE = "Pod Faulted";
    public const string MANAGED_POD_TITLE = "Managed Pod Error";
    public const string UNHANDLED_SERVER_ERROR_TITLE = "Unhandled Server Error";
    public const string UNEXPECTED_ERROR_DETAIL = "Unexpected error while processing the request.";
    public const string PROBLEM_JSON_CONTENT_TYPE = "application/problem+json";
}
