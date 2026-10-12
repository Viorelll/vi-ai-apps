namespace SupportAI.Domain.Models;

public sealed class SupportException(string code, int status = 400, int? retryAfterSeconds = null) : Exception(code)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
    public int? RetryAfterSeconds { get; } = retryAfterSeconds;
}
