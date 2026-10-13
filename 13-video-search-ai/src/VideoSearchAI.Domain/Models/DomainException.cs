namespace VideoSearchAI.Domain.Models;

public sealed class DomainException(string code, int status = 400) : Exception(code)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}
