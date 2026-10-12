namespace VideoStudioAI.Contracts.Dto;

public sealed record RetryJobRequest(Guid RequestId, long Revision, bool AcknowledgeCost);
