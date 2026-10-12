namespace ContractAI.Domain.DTOs;

public sealed record CitationDto(
    Guid ClauseId,
    Guid VersionId,
    Guid DocumentId,
    int Generation,
    int? PageNumber,
    string Excerpt,
    string Url
);
