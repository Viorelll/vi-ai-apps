namespace ProcureAI.Application.Models;

public sealed record StepViewDto(string Role, string State, string Deployment, long InputTokens, long OutputTokens, long DurationMs);
