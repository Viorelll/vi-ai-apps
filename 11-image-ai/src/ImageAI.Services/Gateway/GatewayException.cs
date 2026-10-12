namespace ImageAI.Services.Gateway;

public sealed class GatewayException(
    string code,
    bool retryable = false,
    int retryAfterSeconds = 0,
    bool uncertain = false
) : Exception(code)
{
    public bool Retryable { get; } = retryable;
    public int RetryAfterSeconds { get; } = retryAfterSeconds;
    public bool Uncertain { get; } = uncertain;
}
