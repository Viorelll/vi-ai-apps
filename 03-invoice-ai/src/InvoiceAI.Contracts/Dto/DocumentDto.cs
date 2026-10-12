namespace InvoiceAI.Contracts.Dto;

public sealed record DocumentDto(Guid Id, string FileName, string ContentType, long SizeBytes, string Status, int ActiveGeneration);
