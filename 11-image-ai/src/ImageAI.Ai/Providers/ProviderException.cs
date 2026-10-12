namespace ImageAI.Ai.Providers;

public sealed class ProviderException(string code, int status, int retryAfter = 0) : Exception(code)
{
    public int Status { get; } = status;
    public int RetryAfter { get; } = retryAfter;
}
