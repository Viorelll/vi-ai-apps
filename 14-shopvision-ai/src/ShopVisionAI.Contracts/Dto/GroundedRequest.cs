namespace ShopVisionAI.Contracts.Dto;

public sealed record GroundedRequest(
    Guid OperationId,
    string Question,
    IReadOnlyList<Observation> Observations,
    IReadOnlyList<Candidate> Candidates
);
