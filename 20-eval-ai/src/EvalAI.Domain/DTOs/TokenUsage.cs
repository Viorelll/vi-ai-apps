namespace EvalAI.Domain.DTOs;

public sealed record TokenUsage(long Input, long Output, string Deployment, bool Simulated = false);
