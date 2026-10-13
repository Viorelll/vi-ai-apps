namespace ModerateAI.Domain.Models;

public sealed class DomainException(int status, string code) : Exception(code)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
