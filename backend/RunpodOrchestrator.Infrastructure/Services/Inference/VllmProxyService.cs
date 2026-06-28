using System.Net.Http.Headers;
using RunpodOrchestrator.Application.Exceptions;
using RunpodOrchestrator.Application.Ports.Inference;
using RunpodOrchestrator.Application.Ports.PodLifecycle;
using RunpodOrchestrator.Infrastructure.Configurations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RunpodOrchestrator.Infrastructure.Services.Inference;

public sealed class VllmProxyService : IVllmProxyService
{
    public const string VllmProxyHttpClientName = "VllmProxy";

    private static readonly string[] ForwardedRequestHeaders =
    [
        "Authorization",
        "Accept",
        "Accept-Encoding"
    ];

    private readonly IPodManagerService _podManagerService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RunPodOptions _options;
    private readonly ILogger<VllmProxyService> _logger;

    public VllmProxyService(
        IPodManagerService podManagerService,
        IHttpClientFactory httpClientFactory,
        IOptions<RunPodOptions> options,
        ILogger<VllmProxyService> logger)
    {
        _podManagerService = podManagerService;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ProxyChatCompletionsAsync(
        Stream requestBody,
        IHeaderDictionary requestHeaders,
        HttpResponse response,
        CancellationToken ct = default)
    {
        var isStreaming = requestHeaders.TryGetValue("Accept", out var acceptHeader)
            && acceptHeader.ToString().Contains("text/event-stream", StringComparison.OrdinalIgnoreCase);

        _logger.LogInformation(
            "Received POST /v1/chat/completions (streaming: {IsStreaming}).",
            isStreaming);

        _podManagerService.UpdateLastRequestTime();

        using var bodyBuffer = new MemoryStream();
        await requestBody.CopyToAsync(bodyBuffer, ct).ConfigureAwait(false);
        bodyBuffer.Position = 0;

        var maxAttempts = _options.VllmProxyRetryOnFailure ? 2 : 1;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            bodyBuffer.Position = 0;

            try
            {
                await ProxyOnceAsync(bodyBuffer, requestHeaders, response, ct).ConfigureAwait(false);
                _podManagerService.UpdateLastRequestTime();
                _logger.LogDebug("Updated last request time after proxy completion.");
                return;
            }
            catch (VllmProxyException ex) when (attempt < maxAttempts && IsRetriableProxyFailure(ex))
            {
                _logger.LogWarning(
                    ex,
                    "vLLM proxy attempt {Attempt}/{MaxAttempts} failed. Invalidating ready state and retrying.",
                    attempt,
                    maxAttempts);

                _podManagerService.InvalidateReadyState();
            }
        }
    }

    private async Task ProxyOnceAsync(
        Stream requestBody,
        IHeaderDictionary requestHeaders,
        HttpResponse response,
        CancellationToken ct)
    {
        var baseUrl = await _podManagerService.EnsurePodIsReadyAsync(ct).ConfigureAwait(false);
        var targetUrl = $"{baseUrl.TrimEnd('/')}/v1/chat/completions";

        _logger.LogInformation("Proxying chat completion request to {TargetUrl}.", targetUrl);

        using var request = new HttpRequestMessage(HttpMethod.Post, targetUrl)
        {
            Content = new StreamContent(requestBody)
        };

        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        foreach (var headerName in ForwardedRequestHeaders)
        {
            if (requestHeaders.TryGetValue(headerName, out var headerValue))
            {
                request.Headers.TryAddWithoutValidation(headerName, headerValue.ToArray());
            }
        }

        var httpClient = _httpClientFactory.CreateClient(VllmProxyHttpClientName);

        HttpResponseMessage upstreamResponse;
        try
        {
            upstreamResponse = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "vLLM proxy request failed for {TargetUrl}.", targetUrl);
            throw new VllmProxyException(ApplicationErrorMessages.VllmProxy.REQUEST_FAILED, ex);
        }

        using (upstreamResponse)
        {
            var statusCode = (int)upstreamResponse.StatusCode;

            if (statusCode is 502 or 503 or 504)
            {
                _logger.LogWarning(
                    "vLLM returned retriable status {StatusCode} for {TargetUrl}.",
                    statusCode,
                    targetUrl);

                throw new VllmProxyException(
                    $"{ApplicationErrorMessages.VllmProxy.REQUEST_FAILED} Upstream HTTP {statusCode}.");
            }

            response.StatusCode = statusCode;

            if (upstreamResponse.Content.Headers.ContentType is not null)
            {
                response.ContentType = upstreamResponse.Content.Headers.ContentType.ToString();
            }

            if (statusCode >= 400)
            {
                _logger.LogWarning(
                    "vLLM returned status {StatusCode} for {TargetUrl}.",
                    statusCode,
                    targetUrl);
            }

            await upstreamResponse.Content.CopyToAsync(response.Body, ct).ConfigureAwait(false);
        }
    }

    private static bool IsRetriableProxyFailure(VllmProxyException ex) =>
        ex.InnerException is HttpRequestException or TaskCanceledException
        || ex.Message.Contains("Upstream HTTP 502", StringComparison.Ordinal)
        || ex.Message.Contains("Upstream HTTP 503", StringComparison.Ordinal)
        || ex.Message.Contains("Upstream HTTP 504", StringComparison.Ordinal);
}
