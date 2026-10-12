namespace InvoiceAI.Contracts.Dto;

public sealed record CreateInvoiceRequest(Guid DocumentId, string Kind);
