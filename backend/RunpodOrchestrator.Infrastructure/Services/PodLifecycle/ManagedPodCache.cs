using RunpodOrchestrator.Application.Ports.PodLifecycle;
using Microsoft.Extensions.Logging;

namespace RunpodOrchestrator.Infrastructure.Services.PodLifecycle;

public sealed class ManagedPodCache : IManagedPodCache
{
    private readonly ILogger<ManagedPodCache> _logger;

    public ManagedPodCache(ILogger<ManagedPodCache> logger)
    {
        _logger = logger;
    }

    public string? CachedPodId { get; private set; }

    public void SetCachedPodId(string podId)
    {
        CachedPodId = podId;
    }

    public void Invalidate()
    {
        CachedPodId = null;
        _logger.LogInformation("Managed pod cache invalidated.");
    }
}
