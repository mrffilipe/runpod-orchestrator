using System.Net.Http.Headers;
using RunpodOrchestrator.Application.Ports.RunPod;
using RunpodOrchestrator.Infrastructure.Configurations;
using RunpodOrchestrator.Infrastructure.Services.RunPod;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace RunpodOrchestrator.Infrastructure.Extensions;

public static class RunPodServiceExtensions
{
    public static IServiceCollection AddRunPodClient(this IServiceCollection services)
    {
        services.AddHttpClient<IRunPodApiClient, RunPodApiClient>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<RunPodOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/'));
                client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", options.ApiKey);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2)
            });

        return services;
    }
}
