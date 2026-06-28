namespace RunpodOrchestrator.Domain.Exceptions;

public sealed class PodStartupTimeoutException : DomainException
{
    public PodStartupTimeoutException(string message) : base(message)
    {
    }

    public PodStartupTimeoutException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
