namespace SupportAI.Ai.Models;

public sealed class FoundryOptions
{
    public string Mode { get; set; } = "Foundry";
    public string OpenAiEndpoint { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ChatDeployment { get; set; } = "supportai-chat";
    public int MaxToolRounds { get; set; } = 5;
    public int MaxRunTokens { get; set; } = 15000;
    public int MaxOutputTokens { get; set; } = 1200;
}
