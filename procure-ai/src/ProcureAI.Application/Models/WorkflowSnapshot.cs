namespace ProcureAI.Application.Models;

using ProcureAI.Domain.DTOs;
using ProcureAI.Domain.Entities;

public sealed record WorkflowSnapshot(ProcurementRun Run, IReadOnlyList<AgentStep> Steps,
    IReadOnlyList<SourceDto> Sources, IReadOnlyList<OfferDto> Offers, IReadOnlyList<OfferScoreDto> Scores,
    IReadOnlyList<Guid> OfferDocumentIds);
