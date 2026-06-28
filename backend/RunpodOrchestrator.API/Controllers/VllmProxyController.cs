using RunpodOrchestrator.Application.Ports.Inference;
using Microsoft.AspNetCore.Mvc;

namespace RunpodOrchestrator.API.Controllers;

[ApiController]
[Route("")]
public sealed class VllmProxyController : ControllerBase
{
    private readonly IVllmProxyService _vllmProxyService;
    private readonly IVllmModelsService _vllmModelsService;

    public VllmProxyController(
        IVllmProxyService vllmProxyService,
        IVllmModelsService vllmModelsService)
    {
        _vllmProxyService = vllmProxyService;
        _vllmModelsService = vllmModelsService;
    }

    [HttpGet("v1/models")]
    public IActionResult ListModels() => Ok(_vllmModelsService.GetConfiguredModels());

    [HttpPost("v1/chat/completions")]
    public Task ChatCompletionsAsync(CancellationToken ct) =>
        _vllmProxyService.ProxyChatCompletionsAsync(
            Request.Body,
            Request.Headers,
            Response,
            ct);
}
