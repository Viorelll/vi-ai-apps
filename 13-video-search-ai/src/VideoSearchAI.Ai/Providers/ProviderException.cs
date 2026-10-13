namespace VideoSearchAI.Ai.Providers;

public sealed class ProviderException(string code, int status = 502, TimeSpan? retryAfter = null)
    : Exception(code)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
