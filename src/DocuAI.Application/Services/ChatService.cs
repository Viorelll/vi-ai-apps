using DocuAI.Application.Abstractions;
using DocuAI.Application.Exceptions;
using DocuAI.Domain.DTOs;

namespace DocuAI.Application.Services;

public sealed class ChatService(IKnowledgeRepository knowledge, IAnswerGenerator answerGenerator)
{
    public async Task<AskResponse> AskAsync(AskRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question) || request.Question.Length > 4000)
        {
            throw new ChatValidationException("Question must contain 1 to 4000 characters.");
        }

        var terms = request.Question.Trim().ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => term.Length > 2)
            .Distinct()
            .Take(20)
            .ToArray();

        var chunks = await knowledge.GetReadyChunksAsync(request.DocumentIds, cancellationToken);
        var hits = chunks
            .Select(chunk => new { Chunk = chunk, Score = terms.Count(term => Matches(chunk.Text, term)) })
            .Where(result => result.Score > 0)
            .OrderByDescending(result => result.Score)
            .Take(6)
            .Select(result => result.Chunk)
            .ToArray();

        var citations = hits.Select((chunk, index) => new CitationDto(
            $"S{index + 1}",
            chunk.DocumentId,
            chunk.Id,
            chunk.Document.FileName,
            chunk.PageNumber,
            chunk.StartMs,
            chunk.EndMs,
            chunk.Text[..Math.Min(500, chunk.Text.Length)],
            $"/api/documents/{chunk.DocumentId}/content")).ToArray();

        var answer = hits.Length == 0
            ? "Nu am găsit informații relevante în documentele procesate pentru această întrebare."
            : await answerGenerator.GenerateAsync(
                request.Question,
                hits.Select((chunk, index) => ($"S{index + 1}", chunk.Document.FileName, chunk.Text)).ToArray(),
                cancellationToken);

        return new AskResponse(request.ConversationId ?? Guid.NewGuid(), answer, citations, 0, 0, 0);
    }

    private static bool Matches(string text, string term) => text
        .ToLowerInvariant()
        .Split([' ', '\t', '\r', '\n', '.', ',', ':', ';', '!', '?', '(', ')', '[', ']', '"', '\''])
        .Any(word => word == term ||
                     (term.Length > 5 && word.Length > 5 && word[..5] == term[..5]));
}

