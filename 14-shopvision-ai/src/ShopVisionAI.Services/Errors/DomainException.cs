namespace ShopVisionAI.Services.Errors;

public sealed class DomainException(string code, int status = 400) : Exception(code)
{
    public int Status { get; } = status;
}
