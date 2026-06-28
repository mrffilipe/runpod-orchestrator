using Microsoft.AspNetCore.Http;

namespace RunpodOrchestrator.Application.Ports.Inference;

public interface IVllmProxyService
{
    Task ProxyChatCompletionsAsync(
        Stream requestBody,
        IHeaderDictionary requestHeaders,
        HttpResponse response,
        CancellationToken ct = default);
}
