namespace EvalAI.Domain.DTOs;

public sealed record AiStepResult(string OutputJson, TokenUsage Usage, long DurationMs);
