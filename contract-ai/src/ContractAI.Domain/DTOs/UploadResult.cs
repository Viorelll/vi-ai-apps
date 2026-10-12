namespace ContractAI.Domain.DTOs;

public sealed record UploadResult(Guid DocumentId, Guid? JobId, bool Existing);
