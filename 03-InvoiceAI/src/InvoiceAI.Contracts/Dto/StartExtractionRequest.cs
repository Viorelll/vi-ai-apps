namespace InvoiceAI.Contracts.Dto;

public sealed record StartExtractionRequest(Guid DocumentId, string ObjectKey, string ContentType, string Kind);
