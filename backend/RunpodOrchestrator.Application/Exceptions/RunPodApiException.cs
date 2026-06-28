namespace RunpodOrchestrator.Application.Exceptions;

public sealed class RunPodApiException : Exception
{
    public RunPodApiException(string message) : base(message)
    {
    }

    public RunPodApiException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
