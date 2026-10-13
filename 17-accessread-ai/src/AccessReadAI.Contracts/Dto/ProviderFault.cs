namespace AccessReadAI.Contracts;

public sealed class ProviderFault(string code, bool transient = false, int retryAfterSeconds = 0)
    : Exception(code)
{
    public bool Transient { get; } = transient;
    public int RetryAfterSeconds { get; } = Math.Clamp(retryAfterSeconds, 0, 300);
}
