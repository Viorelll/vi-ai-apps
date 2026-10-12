namespace InvoiceAI.Contracts.Dto;

public sealed record EvidenceDto(Guid DocumentId, string SourceId, int? PageNumber, string Text, string? RegionJson);
