namespace VideoStudioAI.Contracts.Dto;

public sealed record GroundedRequest(string Question, SourceDto[] Sources);
