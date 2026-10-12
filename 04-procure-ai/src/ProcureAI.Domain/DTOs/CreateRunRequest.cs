namespace ProcureAI.Domain.DTOs;

public sealed record CreateRunRequest(string Brief, CriteriaDto Criteria, long BudgetTokens = 15000);
