namespace ContractAI.Domain.DTOs;

public sealed record ClausePair(
    Guid? LeftClauseId,
    Guid? RightClauseId,
    string Kind,
    string? BeforeText,
    string? AfterText
);
