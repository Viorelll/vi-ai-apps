namespace ContractAI.Domain.DTOs;

public sealed record AnalysisResult(
    string Summary,
    string[] Unknowns,
    ObligationCandidate[] Obligations,
    FindingCandidate[] Findings
);
