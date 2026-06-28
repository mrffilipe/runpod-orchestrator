namespace RunpodOrchestrator.Infrastructure.Configurations;

internal static class VllmModelIdHelper
{
    public static string GetPrimaryModelId(RunPodOptions options) =>
        options.VllmDefaultModel
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .First();

    public static IReadOnlyList<string> GetAllModelIds(RunPodOptions options) =>
        options.VllmDefaultModel
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
