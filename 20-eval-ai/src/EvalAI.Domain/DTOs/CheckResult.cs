namespace EvalAI.Domain.DTOs;

public sealed record CheckResult(bool Passed, bool JsonFieldsPassed, bool CitationsPassed, string[] Errors);
