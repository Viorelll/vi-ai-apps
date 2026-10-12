namespace SupportAI.Domain.DTOs;

public sealed record DomainStepResult(AiAnswer Output, string Deployment, long InputTokens, long OutputTokens, long DurationMs);
