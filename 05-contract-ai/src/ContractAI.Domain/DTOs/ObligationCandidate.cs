namespace ContractAI.Domain.DTOs;

public sealed record ObligationCandidate(
    Guid ClauseId,
    string Party,
    string Action,
    DateOnly? DueDate,
    string? RelativeTerm,
    Evidence[] Evidence
);
