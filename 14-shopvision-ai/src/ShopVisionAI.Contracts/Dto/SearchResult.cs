namespace ShopVisionAI.Contracts.Dto;

public sealed record SearchResult(
    IReadOnlyList<Observation> Observations,
    IReadOnlyList<Candidate> Candidates,
    string Warning,
    string CatalogVersion,
    bool Demonstration
);
