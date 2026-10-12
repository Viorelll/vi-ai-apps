namespace ContractAI.Domain.DTOs;

public sealed record CompareRequest(Guid LeftVersionId, Guid RightVersionId);
