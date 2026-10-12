namespace DocuAI.Ai.Services;

public sealed class ContentUnderstandingOptions
{
    public const string SectionName = "ContentUnderstanding";
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string Analyzer { get; set; } = "prebuilt-documentSearch";
}
