namespace InspectAI.Domain.Entities;

public sealed class InspectionReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InspectionId { get; set; }
    public string InputHash { get; set; } = "";
    public string PromptVersion { get; set; } = "inspect-v1";
    public string ResultJson { get; set; } = "{}";
    public string SourcesJson { get; set; } = "[]";
    public string Deployment { get; set; } = "";
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
