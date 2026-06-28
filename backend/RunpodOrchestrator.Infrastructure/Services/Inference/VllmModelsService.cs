using RunpodOrchestrator.Application.Common.OpenAi;
using RunpodOrchestrator.Application.Ports.Inference;
using RunpodOrchestrator.Infrastructure.Configurations;
using Microsoft.Extensions.Options;

namespace RunpodOrchestrator.Infrastructure.Services.Inference;

public sealed class VllmModelsService : IVllmModelsService
{
    private readonly RunPodOptions _options;

    public VllmModelsService(IOptions<RunPodOptions> options)
    {
        _options = options.Value;
    }

    public OpenAiModelsListResponse GetConfiguredModels()
    {
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var modelIds = VllmModelIdHelper.GetAllModelIds(_options);

        var models = modelIds
            .Select(modelId => new OpenAiModelItem
            {
                Id = modelId,
                Created = created
            })
            .ToList();

        return new OpenAiModelsListResponse { Data = models };
    }
}
