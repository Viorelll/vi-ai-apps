namespace InvoiceAI.Infrastructure.Ai;

public sealed class GatewayException(string code, bool transient, int retryAfterSeconds = 2, long inputTokens = 0, long outputTokens = 0) : Exception(code)
{
    public long InputTokens { get; } = inputTokens;
    public long OutputTokens { get; } = outputTokens;
    public string Code { get; } = code;
    public bool Transient { get; } = transient;
    public int RetryAfterSeconds { get; } = retryAfterSeconds;
}
