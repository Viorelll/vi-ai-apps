namespace SupportAI.Domain.DTOs;

public sealed record ChatRequest(string Question, Guid? CaseId);
