namespace AccessReadAI.Contracts;

public sealed record ExtractedBlock(
    string SourceId,
    int Ordinal,
    int PageNumber,
    string Kind,
    string Text,
    string EvidenceJson,
    bool Uncertain = false,
    string? ImageObjectKey = null
);
