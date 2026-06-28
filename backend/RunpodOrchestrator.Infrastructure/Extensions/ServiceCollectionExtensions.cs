using RunpodOrchestrator.Infrastructure.Configurations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RunpodOrchestrator.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptionsWithValidateOnStart<RunPodOptions, RunPodOptionsValidator>()
            .Bind(configuration.GetSection(RunPodOptions.SECTION));

        services.AddRunPodClient();
        services.AddPodLifecycle();
        services.AddInferenceProxy();

        return services;
    }
}
