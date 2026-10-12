namespace ProcureAI.Domain.DTOs;

public sealed record DomainStepResult(string Status, string OutputJson, string Deployment,
    long InputTokens, long OutputTokens, long DurationMs);
