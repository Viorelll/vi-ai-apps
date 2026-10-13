namespace AccessReadAI.Contracts;

public sealed record TransformResult(
    string SourceId,
    string? Simplified,
    string? Translated,
    string? ImageDescription,
    string[] Unknowns,
    string Deployment,
    long InputTokens = 0,
    long OutputTokens = 0
);
