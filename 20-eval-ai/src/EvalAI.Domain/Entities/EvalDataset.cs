namespace EvalAI.Domain.Entities;

public sealed class EvalDataset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = ""; public string Version { get; set; } = ""; public string InputHash { get; set; } = ""; public string EvidenceHash { get; set; } = ""; public string EvidenceJson { get; set; } = "[]"; public int Revision { get; set; } = 1; public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
