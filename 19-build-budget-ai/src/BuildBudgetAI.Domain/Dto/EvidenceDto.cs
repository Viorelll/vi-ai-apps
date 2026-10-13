namespace BuildBudgetAI.Domain.Dto;

public sealed record EvidenceDto(string SourceId, Guid DocumentId, int? PageNumber, string Text);
