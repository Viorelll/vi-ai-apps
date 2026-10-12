namespace ProcureAI.Application.Abstractions;

using ProcureAI.Domain.DTOs;
using ProcureAI.Domain.Entities;
using ProcureAI.Application.Models;

public interface IProcurementRepository
{
    Task<ProcurementRun> CreateAsync(CreateRunRequest request, CancellationToken ct);
    Task<IReadOnlyList<RunSummaryDto>> ListRunsAsync(CancellationToken ct);
    Task<RunViewDto?> GetRunAsync(Guid id, CancellationToken ct);
    Task AttachAsync(Guid id, AttachDocumentRequest request, CancellationToken ct);
    Task<Guid> StartAsync(Guid id, long revision, bool retry, CancellationToken ct);
    Task<WorkflowSnapshot> SnapshotAsync(Guid id, CancellationToken ct);
    Task CommitStepAsync(DurableJob job, AgentStep step, IReadOnlyList<OfferDto>? offers,
        RecommendationDto? recommendation, IReadOnlyList<OfferScoreDto> scores, string state, CancellationToken ct);
    Task StopRunAsync(DurableJob job, string state, string code, CancellationToken ct);
    Task<Document?> GetDocumentAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Document>> ListDocumentsAsync(CancellationToken ct);
    Task<Document?> FindDocumentAsync(string hash, CancellationToken ct);
    Task<Document> AddDocumentAsync(Document document, CancellationToken ct);
    Task RetryDocumentAsync(Guid id, CancellationToken ct);
    Task DeleteDocumentAsync(Guid id, CancellationToken ct);
    Task CompleteDocumentAsync(DurableJob job, string text, CancellationToken ct);
    Task<Guid> SaveChatAsync(ChatRequest request, string answer, IReadOnlyList<SourceDto> sources,
        DomainStepResult result, CancellationToken ct);
}
