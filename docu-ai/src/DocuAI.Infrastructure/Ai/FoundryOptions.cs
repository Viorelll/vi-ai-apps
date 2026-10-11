namespace DocuAI.Infrastructure.Ai;

public sealed class FoundryOptions
{
    public const string SectionName = "Foundry";
    public string? OpenAiEndpoint { get; set; }
    public string? ApiKey { get; set; }
    public string ChatDeployment { get; set; } = "docuai-chat";
}
