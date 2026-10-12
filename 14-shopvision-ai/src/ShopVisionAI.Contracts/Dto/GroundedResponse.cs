namespace ShopVisionAI.Contracts.Dto;

public sealed record GroundedResponse(
    string Answer,
    IReadOnlyList<CandidateExplanation> Explanations,
    IReadOnlyList<string> SourceIds,
    long InputTokens,
    long OutputTokens,
    bool Demonstration
);
