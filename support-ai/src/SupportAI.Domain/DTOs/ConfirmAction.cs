namespace SupportAI.Domain.DTOs;

public sealed record ConfirmAction(long Revision, string PayloadHash, string IdempotencyKey);
