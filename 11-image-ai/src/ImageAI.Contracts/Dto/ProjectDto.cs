namespace ImageAI.Contracts.Dto;

public sealed record ProjectDto(
    Guid Id,
    string Brief,
    string State,
    int Revision,
    IReadOnlyList<AssetDto> Assets,
    IReadOnlyList<RunDto> Runs
);
