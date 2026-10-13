namespace AccessReadAI.Contracts;

public sealed record TransformRequest(
    string SourceId,
    string Text,
    string SourceLanguage,
    string TargetLanguage,
    bool Simplify,
    bool Translate,
    string? ImageObjectKey = null
);
