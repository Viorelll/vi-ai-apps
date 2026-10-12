namespace ContractAI.Domain.DTOs;

public sealed record FindingCandidate(
    string Code,
    string Severity,
    string Explanation,
    Evidence[] Evidence
);
