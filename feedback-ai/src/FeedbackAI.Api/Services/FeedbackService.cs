using FeedbackAI.Application.Abstractions;
using FeedbackAI.Application.Services;
using FeedbackAI.Database.Persistence;
using FeedbackAI.Domain.DTOs;
using FeedbackAI.Domain.Entities;
using FeedbackAI.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

namespace FeedbackAI.Api.Services;

public sealed class FeedbackService(IDbContextFactory<AppDbContext> factory, IClassifier classifier, S3ObjectStore objectStore)
{
    public async Task<BatchDto> CreateAsync(IReadOnlyList<string> messages, CancellationToken ct)
    {
        var clean = messages.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToArray();
        if (clean.Length is 0 or > 500) throw new ArgumentException("messages_count_must_be_1_to_500");
        if (clean.Any(x => x.Length > 12000)) throw new ArgumentException("message_too_long");
        await objectStore.EnsureBucketAsync(ct);
        await using var db = await factory.CreateDbContextAsync(ct);
        var batch = new FeedbackBatch();
        for (var i = 0; i < clean.Length; i++)
        {
            var item = new FeedbackItem { BatchId = batch.Id, Ordinal = i, OriginalText = clean[i], RedactedText = PiiRedactor.Redact(clean[i]) };
            var bytes = System.Text.Encoding.UTF8.GetBytes(clean[i]);
            await using var stream = new MemoryStream(bytes);
            await objectStore.PutAsync($"originals/{batch.Id}/{item.Id}.txt", stream, "text/plain; charset=utf-8", ct);
            batch.Items.Add(item);
        }
        db.Batches.Add(batch);
        await db.SaveChangesAsync(ct);
        return Map(batch);
    }

    public async Task<BatchDto> AnalyzeAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var batch = await db.Batches.Include(x => x.Items).ThenInclude(x => x.Classification).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("batch_not_found");
        foreach (var item in batch.Items)
        {
            var output = await classifier.ClassifyAsync(new ClassificationInput(item.Id, item.RedactedText), ct);
            var topic = Taxonomy.Topics.Contains(output.Topic, StringComparer.OrdinalIgnoreCase) ? output.Topic.ToLowerInvariant() : "unknown";
            var urgency = Taxonomy.Urgencies.Contains(output.Urgency, StringComparer.OrdinalIgnoreCase) ? output.Urgency.ToLowerInvariant() : "unknown";
            var sentiment = new[] { "positive", "neutral", "negative", "unknown" }.Contains(output.Sentiment, StringComparer.OrdinalIgnoreCase) ? output.Sentiment.ToLowerInvariant() : "unknown";
            if (item.Classification is null) item.Classification = new Classification { ItemId = item.Id };
            item.Classification.Topic = topic; item.Classification.Sentiment = sentiment; item.Classification.Urgency = urgency;
            item.Classification.Reason = output.Reason; item.Classification.Provider = output.Provider; item.Classification.ModelVersion = output.ModelVersion;
            item.ActiveTopic = topic; item.ActiveUrgency = urgency;
        }
        batch.State = "Analyzed"; batch.Revision++;
        await db.SaveChangesAsync(ct);
        return Map(batch);
    }

    public async Task<BatchDto> GetAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var batch = await db.Batches.AsNoTracking().Include(x => x.Items).ThenInclude(x => x.Classification).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("batch_not_found");
        return Map(batch);
    }

    public async Task CorrectAsync(Guid itemId, LabelCorrectionRequest request, CancellationToken ct)
    {
        if (!Taxonomy.IsValid(request.Field, request.Value)) throw new ArgumentException("invalid_taxonomy_value");
        await using var db = await factory.CreateDbContextAsync(ct);
        var item = await db.Items.Include(x => x.Batch).SingleOrDefaultAsync(x => x.Id == itemId, ct) ?? throw new KeyNotFoundException("item_not_found");
        if (item.Batch.Revision != request.Revision) throw new InvalidOperationException("revision_conflict");
        var oldValue = request.Field == "topic" ? item.ActiveTopic : item.ActiveUrgency;
        if (request.Field == "topic") item.ActiveTopic = request.Value.ToLowerInvariant(); else item.ActiveUrgency = request.Value.ToLowerInvariant();
        item.Batch.Revision++;
        db.Corrections.Add(new LabelCorrection { ItemId = itemId, Field = request.Field, OldValue = oldValue, NewValue = request.Value.ToLowerInvariant(), Revision = item.Batch.Revision });
        await db.SaveChangesAsync(ct);
    }

    private static BatchDto Map(FeedbackBatch batch)
    {
        var topics = batch.Items.GroupBy(x => x.ActiveTopic).ToDictionary(g => g.Key, g => g.Count());
        return new BatchDto(batch.Id, batch.State, batch.Revision, batch.Items.Count, topics,
            batch.Items.OrderBy(x => x.Ordinal).Select(x => new FeedbackItemDto(x.Id, x.Ordinal, x.RedactedText, x.ActiveTopic, x.Classification?.Topic ?? "unknown", x.Classification?.Sentiment ?? "unknown", x.ActiveUrgency, x.Classification?.Reason ?? "Neanalizat", x.Classification?.Provider ?? "None")).ToArray());
    }
}
