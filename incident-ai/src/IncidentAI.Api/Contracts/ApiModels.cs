namespace IncidentAI.Api.Contracts;

public sealed record CreateIncidentRequest(string Service, DateTimeOffset FromUtc, DateTimeOffset ToUtc);
public sealed record AddReleaseRequest(string Label, DateTimeOffset? DeployedAtUtc);
public sealed record IncidentView(Guid Id, string Service, DateTimeOffset FromUtc, DateTimeOffset ToUtc,
    IReadOnlyList<LogEventView> Events, IReadOnlyList<DiagnosisView> Diagnoses);
public sealed record LogEventView(string SourceId, DateTimeOffset TimestampUtc, string Severity, string? TraceId, string Message);
public sealed record DiagnosisView(Guid Id, DateTimeOffset CreatedAtUtc, string State, string Summary,
    IReadOnlyList<IncidentAI.Application.Contracts.HypothesisResult> Hypotheses, IReadOnlyList<string> NextChecks,
    string Provider, long InputTokens, long OutputTokens, long DurationMs);
