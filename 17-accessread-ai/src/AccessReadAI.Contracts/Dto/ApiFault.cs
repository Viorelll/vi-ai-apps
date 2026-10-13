namespace AccessReadAI.Contracts;

public sealed class ApiFault(int status, string code) : Exception(code)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
