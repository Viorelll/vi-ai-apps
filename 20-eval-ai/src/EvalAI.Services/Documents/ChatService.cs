using System.Text.Json;
using EvalAI.Database.Persistence;
using EvalAI.Domain.DTOs;
using EvalAI.Domain.Entities;
using EvalAI.Domain.Models;
using EvalAI.Domain.Services;
using EvalAI.Infrastructure.Abstractions;
using EvalAI.Services.Evaluations;
using Microsoft.EntityFrameworkCore;
namespace EvalAI.Services.Documents;

public sealed class ChatService(IDbContextFactory<AppDbContext> factory, IAiClient ai)
{
    public async Task<ChatAnswer> AskAsync(ChatRequest request, CancellationToken ct)
    {
        if (request.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(request.Question) || request.Question.Length > 2000 || request.DocumentIds is null || request.DocumentIds.Length is < 1 or > 8) throw new AppException(400, "invalid_question", "Supply requestId, a question and 1-8 ready documents.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var previous = await db.Messages.AsNoTracking().SingleOrDefaultAsync(m => m.RequestId == request.RequestId, ct);
        if (previous != null) { if (previous.Question != request.Question) throw new AppException(409, "request_id_reused", "Request ID used for a different question."); return JsonDefaults.Read<ChatAnswer>(previous.AnswerJson); }
        var chunks = await db.DocumentChunks.Where(c => request.DocumentIds.Contains(c.DocumentId)).Select(c => c.Id).ToArrayAsync(ct);
        var sources = await DatasetService.EvidenceAsync(db, chunks, ct);
        if (sources.Length == 0) throw new AppException(409, "no_evidence", "Selected documents have no ready text.");
        var raw = await ai.ChatAsync(new(request.RequestId, request.Question, sources), ct);
        var check = ScoringRules.Check(raw.OutputJson, "{}", sources); if (!check.CitationsPassed) throw new AppException(502, "invalid_citations", "AI references unknown sources or non-verbatim quotes.");
        using var output = JsonDocument.Parse(raw.OutputJson); var answer = output.RootElement.GetProperty("answer").GetString();
        if (string.IsNullOrWhiteSpace(answer) || answer.Length > 20000) throw new AppException(502, "invalid_answer", "Invalid answer schema.");
        var cited = output.RootElement.GetProperty("citations").EnumerateArray().Select(c => c.GetProperty("chunkId").GetGuid()).ToHashSet();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await DatasetService.EvidenceAsync(db, chunks, ct);
        var conversation = request.ConversationId is { } cid ? await db.Conversations.SingleOrDefaultAsync(c => c.Id == cid, ct) ?? throw new AppException(404, "conversation_not_found", "Conversation not found.") : new Conversation();
        if (request.ConversationId == null) db.Add(conversation);
        var result = new ChatAnswer(conversation.Id, answer, sources.Where(s => cited.Contains(s.ChunkId)).ToArray(), raw.Usage);
        var message = new Message { ConversationId = conversation.Id, RequestId = request.RequestId, Question = request.Question, AnswerJson = JsonDefaults.Serialize(result) }; db.Add(message);
        foreach (var c in result.Citations) db.Add(new Citation { MessageId = message.Id, DocumentId = c.DocumentId, ChunkId = c.ChunkId, Excerpt = c.Text, Generation = c.Generation, PageNumber = c.PageNumber });
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); } catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: "23505" }) { await tx.RollbackAsync(ct); db.ChangeTracker.Clear(); var saved = await db.Messages.SingleAsync(m => m.RequestId == request.RequestId, ct); if (saved.Question != request.Question) throw new AppException(409, "request_id_reused", "Request ID reused."); return JsonDefaults.Read<ChatAnswer>(saved.AnswerJson); }
        return result;
    }
}
