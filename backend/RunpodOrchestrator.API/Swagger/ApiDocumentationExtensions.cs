namespace RunpodOrchestrator.API.Swagger;

public static class ApiDocumentationExtensions
{
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();

        return services;
    }
}
