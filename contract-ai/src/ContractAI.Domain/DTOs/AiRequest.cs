namespace ContractAI.Domain.DTOs;

public sealed record AiRequest(
    string Step,
    Guid OperationId,
    string InputHash,
    string PromptVersion,
    ClauseSource[] Sources,
    string? Question = null,
    ClausePair[]? Pairs = null,
    QuestionHistory[]? History = null
);
