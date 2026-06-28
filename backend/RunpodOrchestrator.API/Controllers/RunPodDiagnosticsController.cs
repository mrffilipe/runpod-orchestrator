using RunpodOrchestrator.Application.Common.RunPod;
using RunpodOrchestrator.Application.Ports.PodLifecycle;
using RunpodOrchestrator.Application.Ports.RunPod;
using RunpodOrchestrator.API.Models;
using RunpodOrchestrator.Infrastructure.Configurations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace RunpodOrchestrator.API.Controllers;

[ApiController]
[Route("v1/runpod")]
public sealed class RunPodDiagnosticsController : ControllerBase
{
    private readonly IRunPodApiClient _runPodApiClient;
    private readonly IPodManagerService _podManagerService;
    private readonly IManagedPodResolver _managedPodResolver;
    private readonly IManagedPodCleanupService _managedPodCleanupService;
    private readonly RunPodOptions _runPodOptions;

    public RunPodDiagnosticsController(
        IRunPodApiClient runPodApiClient,
        IPodManagerService podManagerService,
        IManagedPodResolver managedPodResolver,
        IManagedPodCleanupService managedPodCleanupService,
        IOptions<RunPodOptions> runPodOptions)
    {
        _runPodApiClient = runPodApiClient;
        _podManagerService = podManagerService;
        _managedPodResolver = managedPodResolver;
        _managedPodCleanupService = managedPodCleanupService;
        _runPodOptions = runPodOptions.Value;
    }

    [HttpGet("idle-status")]
    [ProducesResponseType(typeof(PodIdleStatusResponse), StatusCodes.Status200OK)]
    public ActionResult<PodIdleStatusResponse> GetIdleStatus()
    {
        return new PodIdleStatusResponse
        {
            State = _podManagerService.CurrentState,
            LastRequestTime = _podManagerService.GetLastRequestTime(),
            IsPodRunning = _podManagerService.IsPodRunning(),
            IdleTimeoutMinutes = _runPodOptions.PodIdleTimeoutMinutes,
            IdleCheckIntervalSeconds = _runPodOptions.PodIdleCheckIntervalSeconds
        };
    }

    [HttpPost("ensure-ready")]
    [ProducesResponseType(typeof(PodEnsureReadyResponse), StatusCodes.Status200OK)]
    public async Task<PodEnsureReadyResponse> EnsureReadyAsync(CancellationToken ct)
    {
        var endpoint = await _podManagerService.EnsurePodIsReadyAsync(ct);
        return new PodEnsureReadyResponse
        {
            Endpoint = endpoint,
            State = _podManagerService.CurrentState
        };
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(PodStatusResponse), StatusCodes.Status200OK)]
    public async Task<PodStatusResponse> GetStatusAsync(CancellationToken ct)
    {
        var podId = await _managedPodResolver.GetManagedPodIdAsync(ct);
        return await _runPodApiClient.GetPodStatusAsync(podId, ct);
    }

    [HttpPost("resume")]
    [ProducesResponseType(typeof(PodResumeResponse), StatusCodes.Status200OK)]
    public async Task<PodResumeResponse> ResumeAsync([FromQuery] int gpuCount = 1, CancellationToken ct = default)
    {
        var podId = await _managedPodResolver.GetManagedPodIdAsync(ct);
        return await _runPodApiClient.ResumePodAsync(podId, gpuCount, ct);
    }

    [HttpPost("stop")]
    [ProducesResponseType(typeof(PodStopResponse), StatusCodes.Status200OK)]
    public async Task<PodStopResponse> StopAsync(CancellationToken ct)
    {
        var podId = await _managedPodResolver.GetManagedPodIdAsync(ct);
        return await _runPodApiClient.StopPodAsync(podId, ct);
    }

    [HttpPost("terminate-managed")]
    [ProducesResponseType(typeof(PodTerminateManagedResponse), StatusCodes.Status200OK)]
    public async Task<PodTerminateManagedResponse> TerminateManagedAsync(CancellationToken ct)
    {
        var podIds = await _managedPodCleanupService.ListManagedPodIdsAsync(ct);
        await _managedPodCleanupService.TerminateAllManagedPodsAsync(ct: ct);
        _podManagerService.SetPodToStopped();

        return new PodTerminateManagedResponse
        {
            TerminatedPodIds = podIds
        };
    }

    [HttpGet("gpu-availability")]
    [ProducesResponseType(typeof(GpuAvailabilityResponse), StatusCodes.Status200OK)]
    public Task<GpuAvailabilityResponse> GetGpuAvailabilityAsync(
        [FromQuery] string id = "NVIDIA GeForce RTX 4090",
        CancellationToken ct = default) =>
        _runPodApiClient.GetGpuAvailabilityAsync(id, ct);
}
