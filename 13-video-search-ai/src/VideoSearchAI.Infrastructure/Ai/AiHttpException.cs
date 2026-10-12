namespace VideoSearchAI.Infrastructure.Ai;

public sealed class AiHttpException(int status, TimeSpan? retryAfter)
    : Exception($"ai_http_{status}")
{
    public int Status { get; } = status;
    public TimeSpan? RetryAfter { get; } = retryAfter;
    public bool Transient => Status is 408 or 429 || Status >= 500;
}
