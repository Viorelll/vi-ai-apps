namespace VideoStudioAI.Contracts.Dto;

public sealed record RemixRequest(long Revision, Guid RequestId, string Prompt);
