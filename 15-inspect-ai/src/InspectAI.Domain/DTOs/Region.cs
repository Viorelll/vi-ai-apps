namespace InspectAI.Domain.DTOs;

public sealed record Region(
    string CoordinateSpace,
    double X,
    double Y,
    double Width,
    double Height
);
