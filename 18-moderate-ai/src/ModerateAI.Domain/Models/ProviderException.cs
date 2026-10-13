namespace ModerateAI.Domain.Models;

public sealed class ProviderException(string code, bool transient, int retryAfterSeconds = 0)
    : Exception(code)
{
    public string Code { get; } = code;
    public bool Transient { get; } = transient;
    public int RetryAfterSeconds { get; } = retryAfterSeconds;
}
