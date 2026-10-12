using DocuAI.Domain.Entities;

namespace DocuAI.Application.Abstractions;

public interface IKnowledgeRepository
{
    Task<IReadOnlyList<DocumentChunk>> GetReadyChunksAsync(IReadOnlyCollection<Guid>? documentIds, CancellationToken cancellationToken);
}
