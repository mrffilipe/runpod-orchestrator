namespace RunpodOrchestrator.Domain.Exceptions;

public sealed class DomainBusinessRuleException : DomainException
{
    public DomainBusinessRuleException(string message) : base(message)
    {
    }

    public DomainBusinessRuleException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
