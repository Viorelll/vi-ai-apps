namespace EvalAI.Domain.DTOs;

public sealed record AiStepRequest(Guid OperationId, string Step, VariantDefinition Variant, System.Text.Json.JsonElement Input, EvidenceSource[] Sources, long RemainingTokens, string? CandidateOutput = null, System.Text.Json.JsonElement? Expected = null, string RubricVersion = "judge-v1");
