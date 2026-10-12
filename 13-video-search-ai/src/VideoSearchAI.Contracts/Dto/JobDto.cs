namespace VideoSearchAI.Contracts.Dto;

public sealed record JobDto(Guid Id, string Status, string Step, int Attempt, string? ErrorCode);
