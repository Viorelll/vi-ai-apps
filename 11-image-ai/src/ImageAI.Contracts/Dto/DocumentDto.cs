namespace ImageAI.Contracts.Dto;

public sealed record DocumentDto(
    Guid Id,
    string FileName,
    string ContentType,
    string Sha256,
    long SizeBytes,
    int Width,
    int Height,
    bool HasAlpha,
    string Status
);
