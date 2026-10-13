namespace EvalAI.Domain.DTOs;

public sealed record EvalScope(string DatasetVersion, string InputHash, string EvidenceHash, string ChecksVersion, string RubricVersion, string SafetyVersion);
