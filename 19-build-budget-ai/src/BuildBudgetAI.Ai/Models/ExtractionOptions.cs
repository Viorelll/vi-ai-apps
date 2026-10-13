namespace BuildBudgetAI.Ai.Models;

public sealed class ExtractionOptions
{
    public string Endpoint { get; set; } = "https://RESOURCE.cognitiveservices.azure.com";
    public string ApiKey { get; set; } = "dummy-content-understanding-key";
    public string AnalyzerId { get; set; } = "prebuilt-documentSearch";
}
