namespace RunpodOrchestrator.Application.Common.RunPod;

public sealed record PodPortDto
{
    public string? Ip { get; init; }
    public bool? IsPublic { get; init; }
    public int? PrivatePort { get; init; }
    public int? PublicPort { get; init; }
}
