namespace SupportAI.Domain.DTOs;

public sealed record CaseDto(Guid Id, string? OrderReference, string State, long Revision, OrderSnapshot? Order, IReadOnlyList<MessageDto> Messages, IReadOnlyList<Entities.Document> Attachments, IReadOnlyList<Entities.ActionProposal> Proposals, IReadOnlyList<Entities.ActionExecution> Executions, IReadOnlyList<Entities.IngestionJob> Jobs);
