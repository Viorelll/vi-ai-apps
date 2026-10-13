using AccessReadAI.Contracts;
using AccessReadAI.Database;
using AccessReadAI.Domain.Entities;
using AccessReadAI.Services.Clients;
using Microsoft.EntityFrameworkCore;

namespace AccessReadAI.Services.Chat;

public sealed class ChatService(AppDbContext db, DomainAiClient ai)
{
    public async Task<object> AskAsync(ChatRequest request, CancellationToken ct)
    {
        if (
            request.RequestId == Guid.Empty
            || string.IsNullOrWhiteSpace(request.Question)
            || request.Question.Length > 2000
            || request.DocumentIds is null or { Length: 0 or > 8 }
        )
            throw new ApiFault(400, "invalid_chat_request");
        var previous = await db
            .Messages.Include(x => x.Citations)
            .SingleOrDefaultAsync(x => x.RequestId == request.RequestId, ct);
        if (previous != null)
        {
            if (
                previous.Citations.Any(x => !x.DocumentId.HasValue)
                || await db.Documents.AnyAsync(
                    x =>
                        previous.Citations.Select(c => c.DocumentId).Contains(x.Id)
                        && x.State != "Ready",
                    ct
                )
            )
                throw new ApiFault(409, "source_changed");
            return View(previous);
        }
        var conversation = request.ConversationId.HasValue
            ? await db.Conversations.FindAsync([request.ConversationId], ct)
                ?? throw new ApiFault(404, "conversation_not_found")
            : new Conversation();
        if (!request.ConversationId.HasValue)
            db.Conversations.Add(conversation);
        var chunks = await db
            .DocumentChunks.Where(x =>
                request.DocumentIds.Contains(x.DocumentId)
                && db.Documents.Any(d =>
                    d.Id == x.DocumentId && d.State == "Ready" && d.ActiveGeneration == x.Generation
                )
            )
            .OrderBy(x => x.DocumentId)
            .ThenBy(x => x.Ordinal)
            .Take(50)
            .ToListAsync(ct);
        var words = request
            .Question.ToLowerInvariant()
            .Split([' ', '?', '.', ','], StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x.Length > 3)
            .ToArray();
        var selected = chunks
            .Where(x => words.Any(w => x.Text.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(x =>
                words.Count(w => x.Text.Contains(w, StringComparison.OrdinalIgnoreCase))
            )
            .Take(6)
            .ToList();
        var sources = selected.Select((x, i) => new GroundedSource($"S{i + 1}", x.Text)).ToList();
        var answer =
            sources.Count == 0
                ? new GroundedAnswer("Nu există informații suficiente în sursele selectate.", [])
                : await ai.AnswerAsync(new(request.Question, sources), ct);
        if (answer.Labels.Any(x => !sources.Any(s => s.Label == x)))
            throw new ApiFault(502, "invalid_citation");
        // Revalidate after a long model request, so deleted sources cannot enter the response.
        if (
            await db.Documents.AnyAsync(
                d => request.DocumentIds.Contains(d.Id) && d.State == "Deleting",
                ct
            )
        )
            throw new ApiFault(409, "source_changed");
        var message = new Message
        {
            ConversationId = conversation.Id,
            RequestId = request.RequestId,
            Question = request.Question,
            Answer = answer.Answer,
            InputTokens = answer.InputTokens,
            OutputTokens = answer.OutputTokens,
        };
        foreach (var label in answer.Labels.Distinct())
        {
            var index = sources.FindIndex(x => x.Label == label);
            var chunk = selected[index];
            message.Citations.Add(
                new()
                {
                    DocumentId = chunk.DocumentId,
                    ChunkId = chunk.Id,
                    Generation = chunk.Generation,
                    PageNumber = chunk.PageNumber,
                    Label = label,
                    Excerpt = chunk.Text,
                }
            );
        }
        db.Messages.Add(message);
        await db.SaveChangesAsync(ct);
        return View(message);
    }

    private static object View(Message m) =>
        new
        {
            conversationId = m.ConversationId,
            answer = m.Answer,
            citations = m.Citations.Select(c => new
            {
                c.Label,
                c.DocumentId,
                c.PageNumber,
                c.Excerpt,
                contentUrl = c.DocumentId.HasValue
                    ? $"/api/documents/{c.DocumentId}/content#page={c.PageNumber}"
                    : null,
            }),
            m.InputTokens,
            m.OutputTokens,
        };
}
