namespace ImageAI.Domain.Entities;

public sealed class GenerationSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RunId { get; set; }
    public string Deployment { get; set; } = "";
    public string PromptVersion { get; set; } = "image-prompt-v1";
    public string? UsageJson { get; set; }
    public string SafetyStatus { get; set; } = "ProviderFiltered";
    public string? ProviderRequestId { get; set; }
    public string ParametersJson { get; set; } = "{}";
}
