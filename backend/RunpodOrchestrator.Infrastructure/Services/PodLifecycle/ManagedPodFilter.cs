using RunpodOrchestrator.Application.Common.RunPod;
using RunpodOrchestrator.Infrastructure.Configurations;

namespace RunpodOrchestrator.Infrastructure.Services.PodLifecycle;

internal static class ManagedPodFilter
{
    public static bool IsManaged(PodSummaryResponse pod, RunPodOptions options) =>
        string.Equals(pod.Name, options.PodName, StringComparison.OrdinalIgnoreCase);
}
