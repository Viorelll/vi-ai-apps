namespace InvoiceAI.Ai.Configuration;

public sealed class ProviderOptions
{
    public string Mode { get; set; } = "Demo";
    public string DocumentEndpoint { get; set; } = "https://RESOURCE.cognitiveservices.azure.com/";
    public string DocumentKey { get; set; } = "dummy-document-intelligence-key";
    public string OpenAiEndpoint { get; set; } = "https://RESOURCE.openai.azure.com/openai/v1/";
    public string ApiKey { get; set; } = "dummy-foundry-key";
    public string ChatDeployment { get; set; } = "invoiceai-chat";
    public string PromptVersion { get; set; } = "invoice-explain-v1";
    public int MaxToolRounds { get; set; } = 5;
    public int MaxToolCalls { get; set; } = 5;
    public int MaxRunTokens { get; set; } = 15000;
    public int MaxOutputTokens { get; set; } = 1200;
    public bool Configured => Mode == "Demo" || (Valid(DocumentEndpoint, DocumentKey) && Valid(OpenAiEndpoint, ApiKey) && !string.IsNullOrWhiteSpace(ChatDeployment));
    private static bool Valid(string endpoint, string key) => Uri.TryCreate(endpoint, UriKind.Absolute, out var u) && u.Scheme == "https" && !u.Host.StartsWith("RESOURCE", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(key) && !key.StartsWith("dummy", StringComparison.OrdinalIgnoreCase);
}
