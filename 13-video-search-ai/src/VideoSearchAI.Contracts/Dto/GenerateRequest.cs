namespace VideoSearchAI.Contracts.Dto;

public sealed record GenerateRequest(string Question, GroundingSource[] Sources);
