namespace RunpodOrchestrator.Application.Common.RunPod;

public sealed record PodDeployRequest
{
    public required string GpuTypeId { get; init; }
    public required string Name { get; init; }
    public required int GpuCount { get; init; }
    public string? TemplateId { get; init; }
    public string? ImageName { get; init; }
    public string? Ports { get; init; }
    public string? VolumeMountPath { get; init; }
    public int? VolumeInGb { get; init; }
    public int? ContainerDiskInGb { get; init; }
    public string? NetworkVolumeId { get; init; }
    public IReadOnlyDictionary<string, string> Environment { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
