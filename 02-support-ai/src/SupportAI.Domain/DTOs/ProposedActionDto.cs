namespace SupportAI.Domain.DTOs;

public sealed record ProposedActionDto(string Kind, string Summary, IReadOnlyList<string> EvidenceLabels);
