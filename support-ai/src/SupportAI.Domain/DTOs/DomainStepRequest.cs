namespace SupportAI.Domain.DTOs;

public sealed record DomainStepRequest(Guid CorrelationId, Guid OperationId, Guid TargetId, string Text, OrderSnapshot? Order, IReadOnlyList<SourceDto> Sources, IReadOnlyList<HistoryMessage> History);
