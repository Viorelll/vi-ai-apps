namespace ContractAI.Domain.DTOs;

public sealed record CreateVersionRequest(
    Guid DocumentId,
    string ContractKey,
    string VersionLabel,
    Guid? ParentVersionId
);
