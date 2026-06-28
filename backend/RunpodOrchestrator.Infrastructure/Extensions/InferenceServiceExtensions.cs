using RunpodOrchestrator.Application.Ports.Inference;
using RunpodOrchestrator.Infrastructure.Configurations;
using RunpodOrchestrator.Infrastructure.Services.Inference;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace RunpodOrchestrator.Infrastructure.Extensions;

public static class InferenceServiceExtensions
{
    public static IServiceCollection AddInferenceProxy(this IServiceCollection services)
    {
        services.AddHttpClient(VllmProxyService.VllmProxyHttpClientName, (serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<RunPodOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(options.VllmProxyTimeoutSeconds);
        });

        services.AddScoped<IVllmProxyService, VllmProxyService>();
        services.AddScoped<IVllmModelsService, VllmModelsService>();

        return services;
    }
}
