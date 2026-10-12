namespace InvoiceAI.Contracts.Dto;

public sealed record ExtractionDto(Guid DocumentId, string Provider, string ModelVersion, string RawJson, IReadOnlyList<FieldDto> Fields, IReadOnlyList<LineDto> Lines);
