namespace EvalAI.Domain.DTOs;

public sealed record CreateRunRequest(Guid DatasetId, Guid[] VariantIds, Guid? BaselineRunId = null, long BudgetTokens = 15000);
