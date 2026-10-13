namespace AccessReadAI.Contracts;

public sealed record ExtractionResult(
    string State,
    string? OperationUrl,
    IReadOnlyList<ExtractedBlock> Blocks,
    string Provider
);
