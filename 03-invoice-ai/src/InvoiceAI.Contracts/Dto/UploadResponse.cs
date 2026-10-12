namespace InvoiceAI.Contracts.Dto;

public sealed record UploadResponse(Guid DocumentId, bool Duplicate);
