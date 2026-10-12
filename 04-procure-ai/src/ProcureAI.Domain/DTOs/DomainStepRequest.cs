namespace ProcureAI.Domain.DTOs;

public sealed record DomainStepRequest(Guid CorrelationId, Guid OperationId, string Domain, Guid TargetId,
    string Step, string InputHash, string PromptVersion, AgentInputDto Input, IReadOnlyList<Guid> AllowedDocumentIds,
    int MaxOutputTokens = 1200);
