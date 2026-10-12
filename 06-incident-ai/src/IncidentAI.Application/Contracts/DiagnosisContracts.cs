namespace IncidentAI.Application.Contracts;

public sealed record DiagnosisInput(Guid IncidentId, string Service, DateTimeOffset FromUtc,
    DateTimeOffset ToUtc, IReadOnlyList<LogPoint> Events, IReadOnlyList<ReleasePoint> Releases);
public sealed record LogPoint(string SourceId, DateTimeOffset TimestampUtc, string Severity,
    string? TraceId, string Message);
public sealed record ReleasePoint(string Label, DateTimeOffset? DeployedAtUtc);
public sealed record HypothesisResult(string Claim, string ConfidenceLabel,
    IReadOnlyList<string> EvidenceIds, IReadOnlyList<string> Contradictions);
public sealed record DiagnosisResult(string State, string Summary,
    IReadOnlyList<HypothesisResult> Hypotheses, IReadOnlyList<string> NextChecks,
    string Provider, long InputTokens, long OutputTokens, long DurationMs);

public interface IDiagnosisProvider
{
    Task<DiagnosisResult> DiagnoseAsync(DiagnosisInput input, CancellationToken ct);
}
