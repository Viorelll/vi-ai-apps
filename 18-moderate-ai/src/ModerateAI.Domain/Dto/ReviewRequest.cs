namespace ModerateAI.Domain.Dto;

public sealed record ReviewRequest(long Revision, string Decision, string? Note);
