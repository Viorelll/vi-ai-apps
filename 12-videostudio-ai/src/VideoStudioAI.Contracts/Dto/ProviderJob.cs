namespace VideoStudioAI.Contracts.Dto;

public sealed record ProviderJob(string Id, string Status, string? ErrorCode = null);
