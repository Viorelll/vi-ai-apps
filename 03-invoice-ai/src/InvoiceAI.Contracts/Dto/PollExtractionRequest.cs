namespace InvoiceAI.Contracts.Dto;

public sealed record PollExtractionRequest(Guid DocumentId, string OperationUrl, string Kind);
