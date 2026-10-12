using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using VideoStudioAI.Contracts.Dto;
using VideoStudioAI.Database;
using VideoStudioAI.Domain.Entities;
using VideoStudioAI.Domain.Models;
using VideoStudioAI.Services.Interfaces;
using VideoStudioAI.Services.Models;

namespace VideoStudioAI.Services;

public sealed class DocumentService(AppDbContext db, IObjectStore store, IAiGateway ai)
{
    public async Task<(DocumentDto Document, bool Existing)> UploadAsync(
        Stream input,
        string fileName,
        CancellationToken ct
    )
    {
        var path = Path.GetTempFileName();
        string? key = null;
        try
        {
            await using (var output = File.Create(path))
            {
                var buffer = new byte[81920];
                long size = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, ct)) > 0)
                {
                    size += read;
                    if (size > VideoRules.MaxBytes)
                        throw new AppException(413, "file_too_large", "Maximum upload is 50 MiB.");
                    await output.WriteAsync(buffer.AsMemory(0, read), ct);
                }
            }
            var bytes = await File.ReadAllBytesAsync(path, ct);
            if (bytes.Length == 0)
                throw new AppException(400, "empty_file", "Empty file.");
            var mime = DetectMime(bytes);
            var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            var existing = await db.Documents.SingleOrDefaultAsync(d => d.Sha256 == hash, ct);
            if (existing is not null)
                return (Dto(existing), true);
            var id = Guid.NewGuid();
            key = $"documents/{id}/original";
            await store.PutAsync(key, path, mime, ct);
            var doc = new Document
            {
                Id = id,
                FileName = Path.GetFileName(fileName)[
                    ..Math.Min(Path.GetFileName(fileName).Length, 240)
                ],
                ContentType = mime,
                Sha256 = hash,
                ObjectKey = key,
                SizeBytes = bytes.Length,
            };
            db.Add(doc);
            db.Add(new IngestionJob { DocumentId = id });
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                var winner = await db.Documents.SingleOrDefaultAsync(d => d.Sha256 == hash, ct);
                if (winner is null)
                    throw;
                await store.DeleteAsync(key, ct);
                key = null;
                return (Dto(winner), true);
            }
            key = null;
            return (Dto(doc), false);
        }
        finally
        {
            File.Delete(path);
            if (key is not null)
                await store.DeleteAsync(key, CancellationToken.None);
        }
    }

    public static string DetectMime(byte[] b)
    {
        if (b.AsSpan().StartsWith("%PDF-"u8))
            return "application/pdf";
        if (b.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            return "image/png";
        if (b.Length >= 3 && b[0] == 255 && b[1] == 216 && b[2] == 255)
            return "image/jpeg";
        if (b.Length >= 12 && b.AsSpan(4, 4).SequenceEqual("ftyp"u8))
            return "video/mp4";
        try
        {
            var text = new UTF8Encoding(false, true).GetString(b);
            if (text.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t'))
                throw new Exception();
            return "text/plain";
        }
        catch
        {
            throw new AppException(
                415,
                "unsupported_file",
                "Supported files: UTF-8 text, PDF, PNG, JPEG, MP4."
            );
        }
    }

    public async Task<DocumentDto[]> ListAsync(CancellationToken ct) =>
        (
            await db
                .Documents.Where(d => d.Status != "Deleting")
                .OrderByDescending(d => d.CreatedAtUtc)
                .Take(100)
                .ToArrayAsync(ct)
        )
            .Select(Dto)
            .ToArray();

    public async Task<Document> FindAsync(Guid id, CancellationToken ct) =>
        await db.Documents.SingleOrDefaultAsync(d => d.Id == id && d.Status != "Deleting", ct)
        ?? throw new AppException(404, "document_missing", "Document not found.");

    public async Task RetryAsync(Guid id, CancellationToken ct)
    {
        var d = await FindAsync(id, ct);
        if (d.Status != "Failed")
            throw new AppException(409, "retry_invalid", "Only failed extraction can be retried.");
        d.Status = "Uploaded";
        d.ErrorCode = null;
        db.Add(new IngestionJob { DocumentId = id });
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var d = await FindAsync(id, ct);
        if (await db.IngestionJobs.AnyAsync(j => j.DocumentId == id && j.State == "Queued", ct))
            throw new AppException(409, "document_busy", "Wait until extraction has finished.");
        d.Status = "Deleting";
        db.Add(new IngestionJob { DocumentId = id, Kind = "Delete" });
        await db.SaveChangesAsync(ct);
    }

    public async Task<ChatResponse> ChatAsync(ChatRequest r, CancellationToken ct)
    {
        ProjectService.RequireText(r.Question, 2000, "question");
        if (r.DocumentIds is null || r.DocumentIds.Length is < 1 or > 8)
            throw new AppException(400, "sources_required", "Select 1–8 ready documents.");
        var ids = r.DocumentIds.Distinct().ToArray();
        var docs = await db
            .Documents.Where(d => ids.Contains(d.Id) && d.Status == "Ready")
            .ToArrayAsync(ct);
        if (docs.Length != ids.Length)
            throw new AppException(409, "sources_unready", "All selected sources must be ready.");
        var chunks = await db
            .Chunks.Where(c => ids.Contains(c.DocumentId))
            .OrderBy(c => c.Ordinal)
            .Take(128)
            .ToArrayAsync(ct);
        var words = r
            .Question.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x.Length > 2)
            .ToArray();
        var selected = chunks
            .OrderByDescending(c =>
                words.Count(w => c.Text.Contains(w, StringComparison.OrdinalIgnoreCase))
            )
            .Take(8)
            .ToArray();
        var sources = selected
            .Select(c => new SourceDto(
                c.DocumentId,
                c.Id,
                docs.Single(d => d.Id == c.DocumentId).FileName,
                c.Text,
                c.PageNumber
            ))
            .ToArray();
        var answer = await ai.AnswerAsync(new(r.Question, sources), ct);
        if (answer.SourceIds.Any(id => !selected.Any(c => c.Id == id)))
            throw new AppException(
                502,
                "invalid_citation",
                "Provider returned a citation outside the permitted sources."
            );
        if (answer.InputTokens + answer.OutputTokens > 15000)
            throw new AppException(422, "token_budget", "Token budget exceeded.");
        if (
            await db.Documents.CountAsync(d => ids.Contains(d.Id) && d.Status == "Ready", ct)
            != ids.Length
        )
            throw new AppException(409, "sources_changed", "A source changed during processing.");
        var conversation = r.ConversationId is Guid cid
            ? await db.Conversations.FindAsync([cid], ct)
                ?? throw new AppException(404, "conversation_missing", "Conversation not found.")
            : new Conversation();
        if (r.ConversationId is null)
            db.Add(conversation);
        var message = new Message
        {
            ConversationId = conversation.Id,
            Question = r.Question,
            Answer = answer.Answer,
            InputTokens = answer.InputTokens,
            OutputTokens = answer.OutputTokens,
        };
        db.Add(message);
        var cited = sources.Where(s => answer.SourceIds.Contains(s.ChunkId)).ToArray();
        foreach (var s in cited)
            db.Add(
                new Citation
                {
                    MessageId = message.Id,
                    DocumentId = s.DocumentId,
                    ChunkId = s.ChunkId,
                    Excerpt = s.Excerpt,
                    FileName = s.FileName,
                }
            );
        await db.SaveChangesAsync(ct);
        return new(
            conversation.Id,
            answer.Answer,
            cited
                .Select(
                    (s, i) =>
                        new CitationDto(
                            $"S{i + 1}",
                            s.DocumentId,
                            s.ChunkId,
                            s.FileName,
                            s.Excerpt,
                            s.PageNumber,
                            $"/api/documents/{s.DocumentId}/content"
                        )
                )
                .ToArray(),
            answer.InputTokens,
            answer.OutputTokens
        );
    }

    public static DocumentDto Dto(Document d) =>
        new(
            d.Id,
            d.FileName,
            d.ContentType,
            d.SizeBytes,
            d.Status,
            d.ErrorCode,
            $"/api/documents/{d.Id}/content"
        );
}
