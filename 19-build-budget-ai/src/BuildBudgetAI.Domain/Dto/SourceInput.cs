namespace BuildBudgetAI.Domain.Dto;

public sealed record SourceInput(long Revision, Guid DocumentId, string Role);
