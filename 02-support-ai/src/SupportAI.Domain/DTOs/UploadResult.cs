namespace SupportAI.Domain.DTOs;

public sealed record UploadResult(Guid DocumentId, Guid? JobId, string Status, bool Deduplicated);
