namespace RunpodOrchestrator.Application.Exceptions;

public sealed class ManagedPodResolutionException : Exception
{
    public ManagedPodResolutionException(string message) : base(message)
    {
    }

    public ManagedPodResolutionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
