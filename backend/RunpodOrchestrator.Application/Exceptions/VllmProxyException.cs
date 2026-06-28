namespace RunpodOrchestrator.Application.Exceptions;

public sealed class VllmProxyException : Exception
{
    public VllmProxyException(string message) : base(message)
    {
    }

    public VllmProxyException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
