namespace ContractAI.Domain.Entities;

public sealed class Comparison
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LeftVersionId { get; set; }
    public Guid RightVersionId { get; set; }
    public string PipelineVersion { get; set; } = "contract-v1";
    public string InputHash { get; set; } = "";
    public string State { get; set; } = "Queued";
    public string ResultJson { get; set; } = "{}";
    public string? Deployment { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
}
