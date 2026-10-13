namespace AccessReadAI.Contracts;

public sealed record CreateReadingRequest(
    Guid DocumentId,
    string TargetLanguage = "en",
    string SourceLanguage = "ro"
);
