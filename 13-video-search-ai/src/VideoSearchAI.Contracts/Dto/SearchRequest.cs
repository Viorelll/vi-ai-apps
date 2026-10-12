namespace VideoSearchAI.Contracts.Dto;

public sealed record SearchRequest(string Question, Guid[] VideoIds, Guid RequestId);
