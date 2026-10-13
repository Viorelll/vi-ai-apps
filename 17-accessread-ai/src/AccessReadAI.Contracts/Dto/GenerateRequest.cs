namespace AccessReadAI.Contracts;

public sealed record GenerateRequest(
    long Revision,
    bool Simplify = true,
    bool Translate = true,
    bool Narrate = true
);
