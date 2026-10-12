using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SupportAI.Application.Abstractions;
using SupportAI.Database.Persistence;
using SupportAI.Domain.DTOs;
using SupportAI.Domain.Entities;
using SupportAI.Domain.Models;
namespace SupportAI.Application.Services;

public sealed class DurableWorker(IDbContextFactory<AppDbContext> factory, IObjectStore storage, IAiGateway ai, IOrderReader orders, ILogger<DurableWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await ProcessOneAsync(stoppingToken))
                    await Task.Delay(1000, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e) { logger.LogError("Worker failure: {Type}", e.GetType().Name); await Task.Delay(2000, stoppingToken); }
        }
    }
    public async Task<bool> ProcessOneAsync(CancellationToken ct)
    {
        var token = Guid.NewGuid();
        await using var claimDb = await factory.CreateDbContextAsync(ct);
        var claimed = await claimDb.IngestionJobs.FromSqlInterpolated($"""
   UPDATE app."IngestionJobs" SET "Status"='Running', "LeaseToken"={token}, "LeaseUntilUtc"=now()+interval '150 seconds'
   WHERE "Id"=(SELECT "Id" FROM app."IngestionJobs"
    WHERE ("Status" IN ('Queued','RetryWaiting') AND "NextAttemptAtUtc"<=now()) OR ("Status"='Running' AND "LeaseUntilUtc"<now())
    ORDER BY "NextAttemptAtUtc","Id" FOR UPDATE SKIP LOCKED LIMIT 1) RETURNING *
   """).ToListAsync(ct);
        var job = claimed.SingleOrDefault();
        if (job is null)
            return false;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(110));
        try
        {
            switch (job.Kind)
            {
                case "Extract":
                    await ExtractAsync(job, deadline.Token);
                    break;
                case "Support":
                    await RespondAsync(job, deadline.Token);
                    break;
                case "Cleanup":
                    await CleanupAsync(job, deadline.Token);
                    break;
                default:
                    throw new SupportException("unknown_job");
            }
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            var code = e is SupportException s ? s.Code : e is OperationCanceledException ? "provider_timeout" : "processing_failed";
            var transient = e is HttpRequestException or OperationCanceledException || e is SupportException { Status: 429 or >= 500 } && !code.EndsWith("not_configured", StringComparison.Ordinal);
            await using var db = await factory.CreateDbContextAsync(ct);
            var live = await db.IngestionJobs.SingleAsync(x => x.Id == job.Id, ct);
            if (live.LeaseToken != job.LeaseToken)
                return true;
            live.Attempt++;
            live.ErrorCode = code;
            live.LeaseToken = null;
            live.LeaseUntilUtc = null;
            live.Status = transient && live.Attempt < 3 ? "RetryWaiting" : "Failed";
            live.NextAttemptAtUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Max((e as SupportException)?.RetryAfterSeconds ?? 0, Math.Pow(2, live.Attempt) * 3 + Random.Shared.NextDouble()));
            if (live.DocumentId is { } id && live.Status == "Failed")
            {
                var doc = await db.Documents.SingleOrDefaultAsync(x => x.Id == id, ct);
                if (doc is not null && doc.Status != "Deleting")
                {
                    doc.Status = "Failed";
                    doc.ErrorCode = code;
                }
            }
            await db.SaveChangesAsync(ct);
            logger.LogWarning("Job {JobId} ended in {Status}: {Code}", live.Id, live.Status, code);
        }
        return true;
    }
    private async Task<IngestionJob?> LockJobAsync(AppDbContext db, IngestionJob job, CancellationToken ct)
    {
        var live = await db.IngestionJobs.FromSqlInterpolated($"SELECT * FROM app.\"IngestionJobs\" WHERE \"Id\"={job.Id} FOR UPDATE").SingleAsync(ct);
        return live.LeaseToken == job.LeaseToken && live.Status == "Running" && live.LeaseUntilUtc > DateTimeOffset.UtcNow ? live : null;
    }
    private static void Complete(IngestionJob job)
    {
        job.Status = "Complete";
        job.LeaseToken = null;
        job.LeaseUntilUtc = null;
        job.ErrorCode = null;
    }
    private async Task ExtractAsync(IngestionJob job, CancellationToken ct)
    {
        await using var read = await factory.CreateDbContextAsync(ct);
        var doc = await read.Documents.AsNoTracking().SingleAsync(x => x.Id == job.DocumentId, ct);
        if (doc.Status == "Deleting")
            return;
        ExtractionResult result;
        if (doc.ContentType == "text/plain")
        {
            await using var stream = await storage.OpenAsync(doc.ObjectKey, ct);
            using var reader = new StreamReader(stream, new UTF8Encoding(false, true), true);
            result = new("Succeeded", null, [new(await reader.ReadToEndAsync(ct), null, null, null)]);
        }
        else
        {
            result = job.ProviderOperation is null
              ? await ai.StartAsync(new(doc.Id, doc.ObjectKey, doc.ContentType, doc.FileName), ct)
              : await ai.PollAsync(job.ProviderOperation, ct);
        }
        if (result.Status == "Succeeded")
        {
            var prefix = $"artifacts/{doc.Id}/{job.PipelineVersion}";
            await using var normalized = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(result.Sources));
            await storage.PutAsync($"{prefix}/normalized.json", normalized, "application/json", ct);
            if (result.RawJson is not null)
            {
                await using var raw = new MemoryStream(Encoding.UTF8.GetBytes(result.RawJson));
                await storage.PutAsync($"{prefix}/raw.json", raw, "application/json", ct);
            }
        }
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var live = await LockJobAsync(db, job, ct);
        if (live is null)
            return;
        var current = await db.Documents.SingleAsync(x => x.Id == doc.Id, ct);
        if (current.Status == "Deleting")
            return;
        if (result.Status == "Running")
        {
            live.ProviderOperation = result.OperationUrl ?? job.ProviderOperation ?? throw new SupportException("invalid_extraction", 502);
            live.Status = "Queued";
            live.NextAttemptAtUtc = DateTimeOffset.UtcNow.AddSeconds(2);
            live.LeaseToken = null;
            live.LeaseUntilUtc = null;
            current.Status = "Processing";
        }
        else
        {
            if (result.Status != "Succeeded" || result.Sources.Count == 0 || result.Sources.All(x => string.IsNullOrWhiteSpace(x.Text)))
                throw new SupportException("empty_extraction", 422);
            var generation = current.ActiveGeneration + 1;
            var ordinal = 0;
            foreach (var source in result.Sources)
            {
                for (var offset = 0; offset < source.Text.Length; offset += 2400)
                    db.Add(new DocumentChunk { DocumentId = doc.Id, Generation = generation, Ordinal = ordinal++, Text = source.Text.Substring(offset, Math.Min(2800, source.Text.Length - offset)), PageNumber = source.PageNumber, StartMs = source.StartMs, EndMs = source.EndMs });
            }
            current.ActiveGeneration = generation;
            current.Status = "Ready";
            current.ErrorCode = null;
            Complete(live);
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
    private async Task RespondAsync(IngestionJob job, CancellationToken ct)
    {
        await using var read = await factory.CreateDbContextAsync(ct);
        var question = await read.Messages.SingleAsync(x => x.Id == job.TargetId, ct);
        var conversation = await read.Conversations.SingleAsync(x => x.Id == question.ConversationId, ct);
        var item = await read.SupportCases.AsNoTracking().SingleAsync(x => x.Id == conversation.CaseId, ct);
        var attachments = await read.CaseAttachments.Where(x => x.CaseId == item.Id).Select(x => x.DocumentId).ToArrayAsync(ct);
        // NoRag sends a bounded validated source snapshot; it does not pretend to execute vector retrieval.
        var chunks = await (from chunk in read.DocumentChunks
                            join doc in read.Documents on chunk.DocumentId equals doc.Id
                            where doc.Status == "Ready" && chunk.Generation == doc.ActiveGeneration && (doc.IsPolicy || attachments.Contains(doc.Id))
                            orderby doc.IsPolicy descending, chunk.Ordinal
                            select new
                            {
                                chunk,
                                doc.FileName,
                                doc.IsPolicy
                            }).Take(8).ToListAsync(ct);
        var sources = chunks.Select((x, i) => new SourceDto($"S{i + 1}", x.chunk.DocumentId, x.chunk.Id, x.chunk.Generation, x.FileName, x.chunk.Text, x.chunk.PageNumber, x.chunk.StartMs, x.chunk.EndMs, x.IsPolicy)).ToArray();
        var history = await read.Messages.Where(x => x.ConversationId == conversation.Id && !x.Stale && x.Id != question.Id).OrderByDescending(x => x.CreatedAtUtc).Take(10).Select(x => new HistoryMessage(x.Role, x.Text.Length > 1000 ? x.Text.Substring(0, 1000) : x.Text)).ToListAsync(ct);
        history.Reverse();
        var request = new DomainStepRequest(job.Id, job.Id, item.Id, question.Text, orders.Find(item.OrderReference), sources, history);
        var result = await ai.StepAsync(request, ct);
        AnswerValidator.Validate(result.Output, request);
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var live = await LockJobAsync(db, job, ct);
        if (live is null)
            return;
        // Serialize source activation with deletion and reject results from changed generations.
        foreach (var source in sources)
        {
            var doc = await db.Documents.FromSqlInterpolated($"SELECT * FROM app.\"Documents\" WHERE \"Id\"={source.DocumentId} FOR UPDATE").SingleOrDefaultAsync(ct);
            if (doc is null || doc.Status != "Ready" || doc.ActiveGeneration != source.Generation)
                throw new SupportException("stale_sources", 409);
        }
        if (!await db.Messages.AnyAsync(x => x.ConversationId == conversation.Id && x.RequestId == job.Id, ct))
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));
            var message = new Message { ConversationId = conversation.Id, RequestId = job.Id, Role = "assistant", Text = result.Output.Answer, InputTokens = result.InputTokens, OutputTokens = result.OutputTokens, DurationMs = result.DurationMs, Deployment = result.Deployment, InputHash = hash };
            db.Add(message);
            var allLabels = result.Output.CitationLabels.Concat(result.Output.ProposedAction?.EvidenceLabels ?? []).Distinct();
            foreach (var label in allLabels)
            {
                var source = sources.Single(x => x.Label == label);
                db.Add(new Citation { MessageId = message.Id, DocumentId = source.DocumentId, ChunkId = source.ChunkId, Label = source.Label, Excerpt = source.Text, FileName = source.FileName, Generation = source.Generation, PageNumber = source.PageNumber, StartMs = source.StartMs, EndMs = source.EndMs });
            }
            if (result.Output.ProposedAction is { } action)
            {
                var json = JsonSerializer.Serialize(action);
                db.Add(new ActionProposal { CaseId = item.Id, MessageId = message.Id, Kind = action.Kind, ArgumentsJson = json, PayloadHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))) });
            }
            var current = await db.SupportCases.SingleAsync(x => x.Id == item.Id, ct);
            current.State = result.Output.Status switch
            {
                "action_proposed" => "ActionProposed",
                "waiting_for_evidence" => "WaitingForEvidence",
                _ => "Answered"
            };
            current.Revision++;
        }
        Complete(live);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
    private async Task CleanupAsync(IngestionJob job, CancellationToken ct)
    {
        await using var read = await factory.CreateDbContextAsync(ct);
        var doc = await read.Documents.SingleOrDefaultAsync(x => x.Id == job.DocumentId, ct);
        if (doc is not null)
        {
            await storage.DeleteAsync(doc.ObjectKey, ct);
            await storage.DeleteAsync($"artifacts/{doc.Id}/{job.PipelineVersion}/normalized.json", ct);
            await storage.DeleteAsync($"artifacts/{doc.Id}/{job.PipelineVersion}/raw.json", ct);
        }
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var live = await LockJobAsync(db, job, ct);
        if (live is null)
            return;
        if (doc is not null)
        {
            await db.DocumentChunks.Where(x => x.DocumentId == doc.Id).ExecuteDeleteAsync(ct);
            await db.CaseAttachments.Where(x => x.DocumentId == doc.Id).ExecuteDeleteAsync(ct);
            var entity = await db.Documents.SingleAsync(x => x.Id == doc.Id, ct);
            db.Remove(entity);
        }
        Complete(live);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
