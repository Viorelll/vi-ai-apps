namespace BuildBudgetAI.Domain.Dto;

public sealed record UpdateNeedsRequest(long Revision, IReadOnlyList<NeedInput> Needs);
