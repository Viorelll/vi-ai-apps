namespace InspectAI.Domain.DTOs;

public sealed record AddSourceRequest(
    Guid DocumentId,
    string Role,
    int Revision,
    string? ManualModel = null,
    string? ManualVersion = null
);
