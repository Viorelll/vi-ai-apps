namespace BuildBudgetAI.Ai.Models;

public sealed class FoundryOptions
{
    public string Mode { get; set; } = "Demo";
    public string OpenAiEndpoint { get; set; } = "https://RESOURCE.openai.azure.com/openai/v1/";
    public string ApiKey { get; set; } = "dummy-foundry-key";
    public string ChatDeployment { get; set; } = "buildbudgetai-chat";
    public int MaxToolRounds { get; set; } = 5;
    public int MaxRunTokens { get; set; } = 15000;
}
