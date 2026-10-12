namespace VideoStudioAI.Contracts.Dto;

public sealed record ProjectDto(
    Guid Id,
    string Brief,
    string State,
    long Revision,
    bool Confirmed,
    Storyboard? Storyboard,
    JobDto[] Jobs,
    ClipDto[] Clips
);
