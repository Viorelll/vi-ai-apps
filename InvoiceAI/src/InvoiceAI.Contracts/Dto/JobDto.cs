namespace InvoiceAI.Contracts.Dto;

public sealed record JobDto(Guid Id, Guid DocumentId, Guid? TargetId, string Kind, string Status, string Phase, int Attempt, string? ErrorCode);
