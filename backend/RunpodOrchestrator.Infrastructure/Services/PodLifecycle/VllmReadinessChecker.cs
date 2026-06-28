using RunpodOrchestrator.Application.Ports.PodLifecycle;
using RunpodOrchestrator.Domain.Exceptions;
using RunpodOrchestrator.Infrastructure.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RunpodOrchestrator.Infrastructure.Services.PodLifecycle;

public sealed class VllmReadinessChecker : IVllmReadinessChecker
{
    public const string VllmReadinessHttpClientName = "VllmReadiness";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RunPodOptions _options;
    private readonly ILogger<VllmReadinessChecker> _logger;

    public VllmReadinessChecker(
        IHttpClientFactory httpClientFactory,
        IOptions<RunPodOptions> options,
        ILogger<VllmReadinessChecker> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task CheckReadinessAsync(string baseUrl, VllmReadinessMode mode, CancellationToken ct = default)
    {
        var maxAttempts = mode == VllmReadinessMode.Startup
            ? _options.VllmHealthCheckRetries
            : _options.VllmRuntimeHealthRetries;

        var retryDelaySeconds = mode == VllmReadinessMode.Startup
            ? _options.VllmHealthCheckRetryDelaySeconds
            : _options.VllmRuntimeHealthRetryDelaySeconds;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await CheckHealthAsync(baseUrl, attempt, maxAttempts, ct).ConfigureAwait(false);

                if (_options.VllmReadinessRequireModel)
                {
                    await CheckModelsAsync(baseUrl, attempt, maxAttempts, ct).ConfigureAwait(false);
                }

                _logger.LogInformation(
                    "vLLM readiness check succeeded at {BaseUrl} (mode: {Mode}).",
                    baseUrl,
                    mode);

                return;
            }
            catch (DomainBusinessRuleException ex) when (attempt < maxAttempts)
            {
                _logger.LogWarning(
                    ex,
                    "vLLM readiness check failed at {BaseUrl} (attempt {Attempt}/{MaxAttempts}, mode: {Mode}). Retrying in {DelaySeconds}s.",
                    baseUrl,
                    attempt,
                    maxAttempts,
                    mode,
                    retryDelaySeconds);

                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds), ct).ConfigureAwait(false);
            }
        }

        throw new DomainBusinessRuleException(DomainErrorMessages.PodLifecycle.VLLM_HEALTH_CHECK_FAILED);
    }

    private async Task CheckHealthAsync(
        string baseUrl,
        int attempt,
        int maxAttempts,
        CancellationToken ct)
    {
        var healthPath = _options.VllmHealthCheckPath.StartsWith('/')
            ? _options.VllmHealthCheckPath
            : $"/{_options.VllmHealthCheckPath}";

        var healthUrl = $"{baseUrl.TrimEnd('/')}{healthPath}";
        var httpClient = _httpClientFactory.CreateClient(VllmReadinessHttpClientName);

        _logger.LogDebug(
            "vLLM health check attempt {Attempt}/{MaxAttempts} for {HealthUrl}.",
            attempt,
            maxAttempts,
            healthUrl);

        using var response = await httpClient.GetAsync(healthUrl, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new DomainBusinessRuleException(
                $"vLLM health check returned {(int)response.StatusCode} at {healthUrl}.");
        }
    }

    private async Task CheckModelsAsync(
        string baseUrl,
        int attempt,
        int maxAttempts,
        CancellationToken ct)
    {
        var modelsPath = _options.VllmReadinessModelsPath.StartsWith('/')
            ? _options.VllmReadinessModelsPath
            : $"/{_options.VllmReadinessModelsPath}";

        var modelsUrl = $"{baseUrl.TrimEnd('/')}{modelsPath}";
        var httpClient = _httpClientFactory.CreateClient(VllmReadinessHttpClientName);
        var expectedModelId = VllmModelIdHelper.GetPrimaryModelId(_options);

        _logger.LogDebug(
            "vLLM models check attempt {Attempt}/{MaxAttempts} for {ModelsUrl} (expected model: {ExpectedModelId}).",
            attempt,
            maxAttempts,
            modelsUrl,
            expectedModelId);

        using var response = await httpClient.GetAsync(modelsUrl, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new DomainBusinessRuleException(
                $"vLLM models check returned {(int)response.StatusCode} at {modelsUrl}.");
        }

        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!body.Contains(expectedModelId, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainBusinessRuleException(
                $"vLLM models response at {modelsUrl} does not contain expected model '{expectedModelId}'.");
        }
    }
}
