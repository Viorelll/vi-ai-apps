using System.Text.Json;
using AccessReadAI.Contracts;
using AccessReadAI.Database;
using AccessReadAI.Domain.Entities;
using AccessReadAI.Infrastructure.Storage;
using AccessReadAI.Services.Clients;
using AccessReadAI.Services.Readings;
using AccessReadAI.Services.Validation;
using Microsoft.EntityFrameworkCore;

namespace AccessReadAI.Services.Jobs;

public sealed class PipelineProcessor(
    AppDbContext db,
    DomainAiClient ai,
    IObjectStore storage,
    ReadingService readings
)
{
    public async Task ProcessAsync(IngestionJob job, CancellationToken ct)
    {
        var doc =
            await db.Documents.FindAsync([job.DocumentId], ct)
            ?? throw new ProviderFault("document_not_found");
        if (job.Kind == "Cleanup")
        {
            var audio = await db
                .Narration.Where(x =>
                    db.Readings.Any(r => r.Id == x.ReadingId && r.DocumentId == doc.Id)
                )
                .ToListAsync(ct);
            foreach (var segment in audio)
                await storage.DeleteAsync(segment.ObjectKey, ct);
            var images = await db
                .DocumentChunks.Where(x => x.DocumentId == doc.Id && x.ImageObjectKey != null)
                .Select(x => x.ImageObjectKey!)
                .ToListAsync(ct);
            foreach (var key in images)
                await storage.DeleteAsync(key, ct);
            await storage.DeleteAsync(doc.ObjectKey, ct);
            db.Documents.Remove(doc);
            await db.SaveChangesAsync(ct);
            return;
        }
        if (doc.State == "Deleting")
        {
            job.State = "Cancelled";
            await db.SaveChangesAsync(ct);
            return;
        }
        if (job.Kind == "Extract")
        {
            doc.State = "Processing";
            var result = await ai.ExtractAsync(
                new(doc.Id, doc.ObjectKey, doc.ContentType, job.OperationUrl),
                ct
            );
            if (result.State == "Running")
            {
                job.OperationUrl = result.OperationUrl;
                JobStore.Requeue(job, 2);
            }
            else
            {
                if (
                    result.Blocks.Count is 0 or > 100
                    || result.Blocks.Any(x => x.PageNumber < 1 || x.Text.Length > 3000)
                    || result.Blocks.Select(x => x.SourceId).Distinct().Count()
                        != result.Blocks.Count
                )
                    throw new ProviderFault("invalid_extraction");
                var generation = doc.ActiveGeneration + 1;
                foreach (var b in result.Blocks)
                    db.DocumentChunks.Add(
                        new DocumentChunk
                        {
                            DocumentId = doc.Id,
                            Generation = generation,
                            Ordinal = b.Ordinal,
                            PageNumber = b.PageNumber,
                            SourceId = b.SourceId,
                            Text = b.Text,
                            Kind = b.Kind,
                            EvidenceJson = b.EvidenceJson,
                            Uncertain = b.Uncertain,
                            ImageObjectKey = b.ImageObjectKey,
                        }
                    );
                doc.ActiveGeneration = generation;
                doc.State = "Ready";
                doc.ErrorCode = null;
                JobStore.Complete(job);
            }
            await db.SaveChangesAsync(ct);
            return;
        }
        var reading = await readings.LoadAsync(job.TargetId!.Value, ct);
        if (reading.State == "Stale" || reading.Generation != doc.ActiveGeneration)
            throw new ProviderFault("source_generation_changed");
        if (job.Kind == "Narrate")
        {
            var variantId = JsonDocument
                .Parse(job.InputJson)
                .RootElement.GetProperty("VariantId")
                .GetGuid();
            var variant = reading.Blocks.SelectMany(x => x.Variants).Single(x => x.Id == variantId);
            if (variant.Kind == "simplified")
            {
                var correctedBlock = reading.Blocks.Single(x => x.Variants.Contains(variant));
                var dependent = correctedBlock.Variants.SingleOrDefault(x =>
                    x.Kind == "translated"
                );
                if (dependent != null)
                {
                    var transformed = await ai.TransformAsync(
                        new(
                            correctedBlock.SourceId,
                            variant.Text,
                            reading.SourceLanguage,
                            reading.TargetLanguage,
                            false,
                            true
                        ),
                        ct
                    );
                    var translated =
                        transformed.Translated
                        ?? throw new ProviderFault("missing_translated_variant");
                    if (!MeaningValidator.IsSafe(variant.Text, translated, false))
                        throw new ProviderFault("meaning_check_failed");
                    dependent.Text = translated;
                    dependent.TextHash = ReadingService.Hash(translated);
                    dependent.Stale = false;
                    reading.InputTokens += transformed.InputTokens;
                    reading.OutputTokens += transformed.OutputTokens;
                    await NarrateAsync(reading, dependent, ct);
                }
                if (correctedBlock.Warning == "translation_requires_regeneration")
                {
                    correctedBlock.Warning = null;
                    correctedBlock.NeedsReview = false;
                }
            }
            await NarrateAsync(reading, variant, ct);
            reading.State = reading.Blocks.Any(x => x.NeedsReview) ? "NeedsReview" : "Ready";
            reading.Revision++;
            JobStore.Complete(job);
            await db.SaveChangesAsync(ct);
            return;
        }
        var options = JsonSerializer.Deserialize<GenerateRequest>(job.InputJson)!;
        var block = reading.Blocks.OrderBy(x => x.Ordinal).FirstOrDefault(x => !x.Completed);
        if (block == null)
        {
            reading.State = reading.Blocks.Any(x => x.NeedsReview) ? "NeedsReview" : "Ready";
            reading.Revision++;
            JobStore.Complete(job);
            await db.SaveChangesAsync(ct);
            return;
        }
        if (reading.InputTokens + reading.OutputTokens > 15000)
            throw new ProviderFault("token_budget_exceeded");
        reading.State = "Simplifying";
        var resultBlock = await ai.TransformAsync(
            new(
                block.SourceId,
                block.OriginalText,
                reading.SourceLanguage,
                reading.TargetLanguage,
                options.Simplify,
                options.Translate,
                block.ImageObjectKey
            ),
            ct
        );
        if (resultBlock.SourceId != block.SourceId)
            throw new ProviderFault("invalid_evidence_id");
        reading.InputTokens += resultBlock.InputTokens;
        reading.OutputTokens += resultBlock.OutputTokens;
        if (reading.InputTokens + reading.OutputTokens > 15000)
            throw new ProviderFault("token_budget_exceeded");
        if (resultBlock.Unknowns.Length > 0)
        {
            block.NeedsReview = true;
            block.Warning = "uncertain_source_or_output";
        }
        if (block.Kind == "image")
        {
            var description = new ImageDescription()
            {
                BlockId = block.Id,
                Text = resultBlock.ImageDescription ?? "unknown",
                EvidenceJson = block.EvidenceJson,
            };
            block.ImageDescriptions.Add(description);
            db.Set<ImageDescription>().Add(description);
        }
        else
        {
            if (options.Simplify && resultBlock.Simplified == null)
                throw new ProviderFault("missing_simplified_variant");
            if (options.Translate && resultBlock.Translated == null)
                throw new ProviderFault("missing_translated_variant");
            if (resultBlock.Simplified != null)
                AddVariant(
                    reading,
                    block,
                    "simplified",
                    reading.SourceLanguage,
                    resultBlock.Simplified,
                    resultBlock.Deployment
                );
            if (resultBlock.Translated != null)
                AddVariant(
                    reading,
                    block,
                    "translated",
                    reading.TargetLanguage,
                    resultBlock.Translated,
                    resultBlock.Deployment
                );
            if (options.Narrate && !block.NeedsReview)
            {
                reading.State = "Narrating";
                var selected =
                    block.Variants.FirstOrDefault(x => x.Kind == "translated")
                    ?? block.Variants.FirstOrDefault();
                if (selected == null)
                {
                    AddVariant(
                        reading,
                        block,
                        "original",
                        reading.SourceLanguage,
                        block.OriginalText,
                        "original"
                    );
                    selected = block.Variants.Single();
                }
                await NarrateAsync(reading, selected, ct);
            }
        }
        block.Completed = true;
        reading.Revision++;
        JobStore.Requeue(job);
        await db.SaveChangesAsync(ct);
    }

    private void AddVariant(
        Reading reading,
        ReadingBlock block,
        string kind,
        string language,
        string text,
        string deployment
    )
    {
        if (text.Length > 3000)
            throw new ProviderFault("variant_too_long");
        if (!MeaningValidator.IsSafe(block.OriginalText, text, language == reading.SourceLanguage))
        {
            block.NeedsReview = true;
            block.Warning = "meaning_check_failed";
        }
        var variant = new TextVariant()
        {
            BlockId = block.Id,
            Kind = kind,
            Language = language,
            Text = text,
            TextHash = ReadingService.Hash(text),
            Deployment = deployment,
        };
        block.Variants.Add(variant);
        db.Variants.Add(variant);
    }

    private async Task NarrateAsync(Reading reading, TextVariant variant, CancellationToken ct)
    {
        if (variant.Narration.Any(x => !x.Stale && x.TextHash == variant.TextHash))
            return;
        var result = await ai.NarrateAsync(new(variant.Text, variant.Language), ct);
        var bytes = Convert.FromBase64String(result.Base64Audio);
        if (
            bytes.Length is < 44 or > 10000000
            || result.ContentType != "audio/wav"
            || !bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8)
        )
            throw new ProviderFault("invalid_audio");
        var key = $"audio/{reading.Id}/{variant.Id}/{variant.TextHash}.wav";
        await storage.PutBytesAsync(key, bytes, result.ContentType, ct);
        var previous = variant.Narration.FirstOrDefault(x => x.TextHash == variant.TextHash);
        if (previous != null)
        {
            previous.Stale = false;
            return;
        }
        var segment = new NarrationSegment()
        {
            ReadingId = reading.Id,
            VariantId = variant.Id,
            ObjectKey = key,
            ContentType = result.ContentType,
            TextHash = variant.TextHash,
            Voice = result.Voice,
        };
        variant.Narration.Add(segment);
        db.Narration.Add(segment);
    }
}
