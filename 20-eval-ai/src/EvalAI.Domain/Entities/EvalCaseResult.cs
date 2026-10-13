namespace EvalAI.Domain.Entities;

public sealed class EvalCaseResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RunId { get; set; }
    public Guid VariantId { get; set; }
    public Guid CaseId { get; set; }
    public string State { get; set; } = "Created"; public string? OutputJson { get; set; }
    public string? ChecksJson { get; set; }
    public string? JudgeJson { get; set; }
    public string? CandidateUsageJson { get; set; }
    public string? JudgeUsageJson { get; set; }
    public long CandidateLatencyMs { get; set; }
    public long JudgeLatencyMs { get; set; }
    public string? ErrorCode { get; set; }
    public int Attempts { get; set; }
}
