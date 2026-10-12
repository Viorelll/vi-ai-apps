namespace DataAI.Domain.Entities;

public sealed class AnalysisRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DatasetId { get; set; }
    public string Question { get; set; } = "";
    public string SpecJson { get; set; } = "{}";
    public string RowsJson { get; set; } = "[]";
    public string Explanation { get; set; } = "";
    public string ResultHash { get; set; } = "";
    public long DatasetRevision { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
