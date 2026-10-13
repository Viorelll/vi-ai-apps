namespace BuildBudgetAI.Domain.Dto;

public sealed record ChatResult(
    Guid ConversationId,
    string Summary,
    IReadOnlyList<string> Unknowns,
    IReadOnlyList<EvidenceDto> Citations,
    string Deployment
);
