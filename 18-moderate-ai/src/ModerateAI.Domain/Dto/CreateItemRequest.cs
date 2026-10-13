namespace ModerateAI.Domain.Dto;

public sealed record CreateItemRequest(Guid RequestId, string? Text, Guid? ImageDocumentId);
