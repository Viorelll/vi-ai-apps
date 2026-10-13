namespace EvalAI.Domain.DTOs;

public sealed record ChatAiRequest(Guid OperationId, string Question, EvidenceSource[] Sources);
