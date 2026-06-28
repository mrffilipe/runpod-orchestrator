namespace RunpodOrchestrator.Domain.Exceptions;

public sealed class DomainNotFoundException : DomainException
{
    public DomainNotFoundException(string message) : base(message)
    {
    }

    public DomainNotFoundException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
