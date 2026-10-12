namespace ShopVisionAI.Contracts.Dto;

public sealed record VisionResponse(
    string Status,
    IReadOnlyList<Observation> Observations,
    string Deployment,
    long InputTokens,
    long OutputTokens,
    bool Demonstration
);
