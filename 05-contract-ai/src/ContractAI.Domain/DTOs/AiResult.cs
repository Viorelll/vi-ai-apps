namespace ContractAI.Domain.DTOs;

public sealed record AiResult(
    string OutputJson,
    string Deployment,
    long InputTokens,
    long OutputTokens,
    long DurationMs
);
