namespace InspectAI.Domain.DTOs;

public sealed record InspectionResult(
    string Summary,
    string[] Unknowns,
    FindingCandidate[] Findings,
    ChecklistCandidate[] Checklist
);
