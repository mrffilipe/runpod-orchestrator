namespace RunpodOrchestrator.Domain.Exceptions;

public sealed class DomainValidationException : DomainException
{
    public DomainValidationException(string message) : base(message)
    {
    }

    public DomainValidationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
