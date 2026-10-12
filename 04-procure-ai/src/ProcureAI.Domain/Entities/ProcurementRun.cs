namespace ProcureAI.Domain.Entities;

public sealed class ProcurementRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Brief { get; set; }
    public required string CriteriaJson { get; set; }
    public string SnapshotHash { get; set; } = "";
    public string State { get; set; } = "Created";
    public long Revision { get; set; }
    public long BudgetTokens { get; set; } = 15000;
    public long UsedTokens { get; set; }
    public bool Stale { get; set; }
    public string? ErrorCode { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
