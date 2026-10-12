namespace InvoiceAI.Services.Exceptions;

public sealed class AppException(string code, int status = 400) : Exception(code)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}
