namespace InvoiceAI.Contracts.Dto;

public sealed record CitationDto(string SourceId, Guid DocumentId, string FileName, int? PageNumber, string Excerpt, string ContentUrl);
