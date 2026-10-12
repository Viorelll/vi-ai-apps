namespace VideoSearchAI.Contracts.Dto;

public sealed record SearchRunDto(Guid Id, string State, AnswerDto? Result, string? ErrorCode);
