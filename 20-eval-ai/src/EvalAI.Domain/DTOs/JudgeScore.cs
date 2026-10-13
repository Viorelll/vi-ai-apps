namespace EvalAI.Domain.DTOs;

public sealed record JudgeScore(decimal Score, string Reason, string RubricVersion);
