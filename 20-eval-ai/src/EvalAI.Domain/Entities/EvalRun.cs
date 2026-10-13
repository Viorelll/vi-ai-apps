namespace EvalAI.Domain.Entities;

public sealed class EvalRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DatasetId { get; set; }
    public Guid? BaselineRunId { get; set; }
    public string ScopeJson { get; set; } = "{}"; public string VariantsJson { get; set; } = "[]"; public string RatesJson { get; set; } = "[]"; public string State { get; set; } = "Created"; public int Revision { get; set; } = 1; public long BudgetTokens { get; set; }
    public long UsedTokens { get; set; }
    public string? ErrorCode { get; set; }
    public bool Stale { get; set; }
    public bool PossibleRepeatedCloudCost { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow; public DateTimeOffset DeadlineUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
}
