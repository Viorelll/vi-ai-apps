namespace SupportAI.Domain.DTOs;

public sealed record AiAnswer(string Answer, string Status, IReadOnlyList<string> CitationLabels, ProposedActionDto? ProposedAction);
