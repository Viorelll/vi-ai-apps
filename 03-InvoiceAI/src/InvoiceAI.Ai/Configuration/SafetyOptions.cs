namespace InvoiceAI.Ai.Configuration;

public sealed class SafetyOptions
{
    public bool PromptShieldsEnabled
    {
        get; set;
    }
    public string Endpoint { get; set; } = "https://RESOURCE.cognitiveservices.azure.com/";
    public string ApiKey { get; set; } = "dummy-content-safety-key";
}
