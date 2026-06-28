using RunpodOrchestrator.Application.Common.RunPod;

namespace RunpodOrchestrator.Application.Ports.RunPod;

public interface IRunPodApiClient
{
    Task<PodStatusResponse> GetPodStatusAsync(string podId, CancellationToken ct = default);
    Task<PodResumeResponse> ResumePodAsync(string podId, int gpuCount, CancellationToken ct = default);
    Task<PodStopResponse> StopPodAsync(string podId, CancellationToken ct = default);
    Task<PodTerminateResponse> TerminatePodAsync(string podId, CancellationToken ct = default);
    Task<GpuAvailabilityResponse> GetGpuAvailabilityAsync(string gpuTypeId, CancellationToken ct = default);
    Task<PodListResponse> ListPodsAsync(CancellationToken ct = default);
    Task<PodDeployResponse> DeployPodAsync(PodDeployRequest request, CancellationToken ct = default);
}
