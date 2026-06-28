using RunpodOrchestrator.Application.Common.OpenAi;

namespace RunpodOrchestrator.Application.Ports.Inference;

public interface IVllmModelsService
{
    OpenAiModelsListResponse GetConfiguredModels();
}
