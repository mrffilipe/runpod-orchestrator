namespace RunpodOrchestrator.Domain.Exceptions;

public sealed class PodFaultedException : DomainException
{
    public PodFaultedException(string message) : base(message)
    {
    }

    public PodFaultedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
