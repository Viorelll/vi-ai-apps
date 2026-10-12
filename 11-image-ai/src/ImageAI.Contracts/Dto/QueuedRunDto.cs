namespace ImageAI.Contracts.Dto;

public sealed record QueuedRunDto(Guid ProjectId, Guid RunId, Guid JobId, string State);
