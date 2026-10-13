using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AccessReadAI.Contracts;
using AccessReadAI.Database;
using AccessReadAI.Domain.Entities;
using AccessReadAI.Services.Validation;
using Microsoft.EntityFrameworkCore;

namespace AccessReadAI.Services.Readings;

public sealed class ReadingService(AppDbContext db)
{
    public static string Hash(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    public async Task<Reading> CreateAsync(CreateReadingRequest request, CancellationToken ct)
    {
        LanguageCatalog.Validate(request.SourceLanguage);
        LanguageCatalog.Validate(request.TargetLanguage);
        var doc =
            await db.Documents.FindAsync([request.DocumentId], ct)
            ?? throw new ApiFault(404, "document_not_found");
        if (doc.State == "Deleting")
            throw new ApiFault(409, "document_deleting");
        var reading = new Reading
        {
            DocumentId = doc.Id,
            TargetLanguage = request.TargetLanguage,
            SourceLanguage = request.SourceLanguage,
        };
        db.Readings.Add(reading);
        await db.SaveChangesAsync(ct);
        return reading;
    }

    public async Task<Reading> LoadAsync(Guid id, CancellationToken ct) =>
        await db
            .Readings.Include(x => x.Blocks)
                .ThenInclude(x => x.Variants)
                    .ThenInclude(x => x.Narration)
            .Include(x => x.Blocks)
                .ThenInclude(x => x.ImageDescriptions)
            .SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new ApiFault(404, "reading_not_found");

    public async Task GenerateAsync(Guid id, GenerateRequest request, CancellationToken ct)
    {
        var reading = await LoadAsync(id, ct);
        if (reading.Revision != request.Revision)
            throw new ApiFault(409, "revision_conflict");
        if (reading.State is not ("Uploaded" or "Failed"))
            throw new ApiFault(409, "reading_not_generatable");
        var doc =
            await db.Documents.FindAsync([reading.DocumentId], ct)
            ?? throw new ApiFault(404, "document_not_found");
        if (doc.State != "Ready")
            throw new ApiFault(409, "document_not_ready");
        reading.Generation = doc.ActiveGeneration;
        reading.State = "Simplifying";
        reading.Revision++;
        reading.ErrorCode = null;
        if (reading.Blocks.Count == 0)
        {
            var chunks = await db
                .DocumentChunks.Where(x =>
                    x.DocumentId == doc.Id && x.Generation == doc.ActiveGeneration
                )
                .OrderBy(x => x.Ordinal)
                .ToListAsync(ct);
            reading.Blocks = chunks
                .Select(x => new ReadingBlock
                {
                    ReadingId = reading.Id,
                    ChunkId = x.Id,
                    Ordinal = x.Ordinal,
                    PageNumber = x.PageNumber,
                    SourceId = x.SourceId,
                    OriginalText = x.Text,
                    Kind = x.Kind,
                    EvidenceJson = x.EvidenceJson,
                    ImageObjectKey = x.ImageObjectKey,
                    NeedsReview = x.Uncertain,
                })
                .ToList();
            db.Blocks.AddRange(reading.Blocks);
        }
        db.Jobs.Add(
            new IngestionJob
            {
                DocumentId = doc.Id,
                TargetId = id,
                Kind = "Generate",
                InputJson = JsonSerializer.Serialize(request),
            }
        );
        await db.SaveChangesAsync(ct);
    }

    public async Task CorrectAsync(Guid id, CorrectVariantRequest request, CancellationToken ct)
    {
        var reading = await LoadAsync(id, ct);
        if (reading.Revision != request.Revision)
            throw new ApiFault(409, "revision_conflict");
        if (reading.State is not ("Ready" or "NeedsReview"))
            throw new ApiFault(409, "reading_busy_or_stale");
        var block =
            reading.Blocks.SingleOrDefault(x => x.Variants.Any(v => v.Id == request.VariantId))
            ?? throw new ApiFault(404, "variant_not_found");
        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 3000)
            throw new ApiFault(400, "invalid_variant_text");
        var variant = block.Variants.Single(x => x.Id == request.VariantId);
        if (
            !MeaningValidator.IsSafe(
                block.OriginalText,
                request.Text,
                variant.Language == reading.SourceLanguage
            )
        )
            throw new ApiFault(422, "meaning_check_failed");
        variant.Text = request.Text;
        variant.TextHash = Hash(request.Text);
        foreach (var segment in variant.Narration)
            segment.Stale = true;
        // Correcting the simplified source invalidates dependent translations, never presents old audio as current.
        if (variant.Kind == "simplified")
        {
            foreach (var dependent in block.Variants.Where(x => x.Kind == "translated").ToList())
            {
                dependent.Stale = true;
                foreach (var segment in dependent.Narration)
                    segment.Stale = true;
            }
            block.NeedsReview = true;
            block.Warning = "translation_requires_regeneration";
        }
        reading.State = "Narrating";
        reading.Revision++;
        db.Jobs.Add(
            new IngestionJob
            {
                DocumentId = reading.DocumentId,
                TargetId = id,
                Kind = "Narrate",
                InputJson = JsonSerializer.Serialize(new { VariantId = variant.Id }),
            }
        );
        await db.SaveChangesAsync(ct);
    }
}
