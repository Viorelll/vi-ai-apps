namespace VideoSearchAI.Contracts.Dto;

public sealed record RetrieveRequest(
    string Question,
    Guid[] DocumentIds,
    Dictionary<Guid, int> Generations
);
