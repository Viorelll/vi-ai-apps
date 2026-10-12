namespace VideoStudioAI.Ai.Models;

public sealed class GatewayException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
