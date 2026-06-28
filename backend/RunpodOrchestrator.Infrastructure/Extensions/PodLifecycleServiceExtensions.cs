using RunpodOrchestrator.Application.Ports.PodLifecycle;
using RunpodOrchestrator.Infrastructure.Configurations;
using RunpodOrchestrator.Infrastructure.Services.PodLifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace RunpodOrchestrator.Infrastructure.Extensions;

public static class PodLifecycleServiceExtensions
{
    public static IServiceCollection AddPodLifecycle(this IServiceCollection services)
    {
        services.AddHttpClient(VllmReadinessChecker.VllmReadinessHttpClientName, (serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<RunPodOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(options.VllmHealthCheckTimeoutSeconds);
        });

        services.AddSingleton<IManagedPodCache, ManagedPodCache>();
        services.AddSingleton<IManagedPodCleanupService, ManagedPodCleanupService>();
        services.AddSingleton<IVllmReadinessChecker, VllmReadinessChecker>();
        services.AddSingleton<IPodManagerService, PodManagerService>();
        services.AddSingleton<IManagedPodResolver, ManagedPodResolver>();
        services.AddHostedService<IdleMonitorService>();

        return services;
    }
}
