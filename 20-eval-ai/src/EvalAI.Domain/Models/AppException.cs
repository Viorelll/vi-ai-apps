namespace EvalAI.Domain.Models;

public sealed class AppException(
    int status,
    string code,
    string message,
    int? retryAfterSeconds = null
) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public int? RetryAfterSeconds { get; } = retryAfterSeconds;
}
