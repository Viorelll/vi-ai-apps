namespace InvoiceAI.Contracts.Dto;

public sealed record PollExtractionResponse(string Status, ExtractionDto? Extraction, int RetryAfterSeconds = 2);
