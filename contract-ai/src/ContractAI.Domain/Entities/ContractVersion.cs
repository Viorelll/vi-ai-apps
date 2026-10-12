namespace ContractAI.Domain.Entities;

public sealed class ContractVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public string ContractKey { get; set; } = "";
    public string VersionLabel { get; set; } = "";
    public Guid? ParentVersionId { get; set; }
    public string State { get; set; } = "Uploaded";
    public long Revision { get; set; }
    public string Summary { get; set; } = "";
    public string UnknownsJson { get; set; } = "[]";
    public string? InputHash { get; set; }
    public string PromptVersion { get; set; } = "contract-v1";
    public string? Deployment { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
}
