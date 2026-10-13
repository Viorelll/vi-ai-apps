namespace EvalAI.Domain.DTOs;

public sealed record CreateDatasetRequest(string Name, string Version, CaseInput[] Cases);
