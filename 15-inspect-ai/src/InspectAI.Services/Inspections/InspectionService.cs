using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InspectAI.Database.Persistence;
using InspectAI.Domain.DTOs;
using InspectAI.Domain.Entities;
using InspectAI.Domain.Models;
using InspectAI.Domain.Services;
using InspectAI.Infrastructure.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace InspectAI.Services.Inspections;

public sealed class InspectionService(IDbContextFactory<AppDbContext> factory, IAiClient ai)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Hash(string input) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));

    public async Task<Inspection> CreateAsync(CreateInspectionRequest r, CancellationToken ct)
    {
        if (
            string.IsNullOrWhiteSpace(r.EquipmentLabel)
            || r.EquipmentLabel.Length > 200
            || string.IsNullOrWhiteSpace(r.EquipmentModel)
            || r.EquipmentModel.Length > 100
            || string.IsNullOrWhiteSpace(r.ManualVersion)
            || r.ManualVersion.Length > 100
        )
            throw new AppException(
                400,
                "invalid_inspection",
                "Equipment label, confirmed model and manual version are required."
            );
        await using var db = await factory.CreateDbContextAsync(ct);
        var i = new Inspection
        {
            EquipmentLabel = r.EquipmentLabel.Trim(),
            EquipmentModel = r.EquipmentModel.Trim(),
            ManualVersion = r.ManualVersion.Trim(),
        };
        db.Add(i);
        await db.SaveChangesAsync(ct);
        return i;
    }

    public async Task AddSourceAsync(Guid id, AddSourceRequest r, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var i = await GetEntityAsync(db, id, ct);
        CheckRevision(i, r.Revision);
        if (i.State is "Inspecting" or "Retrieving")
            throw new AppException(409, "inspection_busy", "Wait for the current analysis.");
        var d =
            await db.Documents.SingleOrDefaultAsync(
                x => x.Id == r.DocumentId && x.Status != "Deleting",
                ct
            ) ?? throw new AppException(404, "document_not_found", "Document not found.");
        if (
            r.Role is not ("photo" or "manual")
            || (r.Role == "photo" && !d.ContentType.StartsWith("image/"))
            || (r.Role == "manual" && d.ContentType.StartsWith("image/"))
        )
            throw new AppException(
                400,
                "invalid_source_role",
                "Photos must be PNG/JPEG; manuals must be PDF/DOCX/text."
            );
        if (
            r.Role == "manual"
            && (r.ManualModel != i.EquipmentModel || r.ManualVersion != i.ManualVersion)
        )
            throw new AppException(
                409,
                "manual_mismatch",
                "Confirm the exact equipment model and manual version before attaching the manual."
            );
        if (
            await db.InspectionSources.AnyAsync(
                x => x.InspectionId == id && x.DocumentId == d.Id,
                ct
            )
        )
            return;
        if (
            r.Role == "manual"
            && await db.InspectionSources.AnyAsync(
                x => x.InspectionId == id && x.Role == "manual",
                ct
            )
        )
            throw new AppException(
                409,
                "manual_already_selected",
                "Create a separate inspection for another manual version."
            );
        if (
            r.Role == "photo"
            && await db.InspectionSources.CountAsync(
                x => x.InspectionId == id && x.Role == "photo",
                ct
            ) >= 8
        )
            throw new AppException(400, "photo_limit", "Maximum eight photographs.");
        if (await db.InspectionSources.CountAsync(x => x.InspectionId == id, ct) >= 9)
            throw new AppException(400, "source_limit", "Maximum eight photos and one manual.");
        db.Add(
            new InspectionSource
            {
                InspectionId = id,
                DocumentId = d.Id,
                Role = r.Role,
            }
        );
        i.Revision++;
        i.State = await db.InspectionReports.AnyAsync(x => x.InspectionId == id, ct)
            ? "Stale"
            : "Created";
        await SaveAsync(db, ct);
    }

    public async Task<Guid> AnalyzeAsync(Guid id, AnalyzeRequest r, CancellationToken ct)
    {
        if (r.RequestId == Guid.Empty)
            throw new AppException(400, "request_id_required", "requestId is required.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var existing = await db.IngestionJobs.SingleOrDefaultAsync(
            x => x.Id == r.RequestId && x.TargetId == id && x.Kind == "Analyze",
            ct
        );
        if (existing is not null)
            return existing.Id;
        var i = await GetEntityAsync(db, id, ct);
        CheckRevision(i, r.Revision);
        if (i.State is "Inspecting" or "Retrieving")
            throw new AppException(409, "inspection_busy", "Analysis already running.");
        await ScopeAsync(db, i, ct);
        i.State = "Inspecting";
        i.ErrorCode = null;
        i.Revision++;
        var job = new IngestionJob
        {
            Id = r.RequestId,
            TargetId = id,
            Kind = "Analyze",
        };
        db.Add(job);
        await SaveAsync(db, ct);
        return job.Id;
    }

    public async Task<object> GetAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var i = await GetEntityAsync(db, id, ct);
        var sources = await (
            from s in db.InspectionSources
            join d in db.Documents on s.DocumentId equals d.Id
            where s.InspectionId == id
            select new
            {
                s.Id,
                s.DocumentId,
                s.Role,
                d.FileName,
                d.Status,
                d.ErrorCode,
                d.ActiveGeneration,
                d.Sha256,
                contentUrl = "/api/documents/"
                    + d.Id
                    + (s.Role == "photo" ? "/preview" : "/content"),
            }
        ).ToArrayAsync(ct);
        var findings = await db
            .VisualFindings.Where(x => x.InspectionId == id)
            .OrderBy(x => x.Id)
            .ToArrayAsync(ct);
        var checklist = await db
            .ChecklistItems.Where(x => x.InspectionId == id)
            .OrderBy(x => x.Id)
            .ToArrayAsync(ct);
        var report = await db
            .InspectionReports.Where(x => x.InspectionId == id)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
        var questions = await db
            .QuestionMessages.Where(x => x.InspectionId == id)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new { x.Question, x.AnswerJson })
            .ToArrayAsync(ct);
        return new
        {
            i.Id,
            i.EquipmentLabel,
            i.EquipmentModel,
            i.ManualVersion,
            i.State,
            i.Revision,
            i.ErrorCode,
            sources,
            findings,
            checklist,
            report,
            questions,
            limitations = GroundingValidator.Limitations,
        };
    }

    public async Task<object> ExportAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var i = await GetEntityAsync(db, id, ct);
        if (i.State is not ("Ready" or "NeedsReview"))
            throw new AppException(
                409,
                "report_not_current",
                "Only current, completed reports can be exported."
            );
        await ScopeAsync(db, i, ct);
        return await GetAsync(id, ct);
    }

    public async Task NoteAsync(
        Guid id,
        Guid itemId,
        ChecklistStatusRequest r,
        CancellationToken ct
    )
    {
        if (r.Status is not ("proposed" or "noted"))
            throw new AppException(
                400,
                "invalid_checklist_status",
                "Status must be proposed or noted."
            );
        await using var db = await factory.CreateDbContextAsync(ct);
        var i = await GetEntityAsync(db, id, ct);
        CheckRevision(i, r.Revision);
        if (i.State is not ("Ready" or "NeedsReview"))
            throw new AppException(409, "report_not_current", "Report is not current.");
        var item =
            await db.ChecklistItems.SingleOrDefaultAsync(
                x => x.Id == itemId && x.InspectionId == id,
                ct
            ) ?? throw new AppException(404, "checklist_not_found", "Checklist item not found.");
        item.Status = r.Status;
        i.Revision++;
        await SaveAsync(db, ct);
    }

    public async Task<QuestionResult> QuestionAsync(
        Guid id,
        QuestionRequest r,
        CancellationToken ct
    )
    {
        if (
            r.RequestId == Guid.Empty
            || string.IsNullOrWhiteSpace(r.Question)
            || r.Question.Length > 2000
        )
            throw new AppException(
                400,
                "invalid_question",
                "A question (1–2000 characters) and requestId are required."
            );
        await using var db = await factory.CreateDbContextAsync(ct);
        var i = await GetEntityAsync(db, id, ct);
        if (i.State is not ("Ready" or "NeedsReview"))
            throw new AppException(
                409,
                "report_not_current",
                "Wait for a current inspection report."
            );
        var prior = await db.QuestionMessages.SingleOrDefaultAsync(
            x => x.InspectionId == id && x.RequestId == r.RequestId,
            ct
        );
        if (prior is not null)
        {
            if (prior.Question != r.Question)
                throw new AppException(
                    409,
                    "request_id_conflict",
                    "requestId was already used for a different question."
                );
            return JsonSerializer.Deserialize<QuestionResult>(prior.AnswerJson, Json)!;
        }
        CheckRevision(i, r.Revision);
        var (photos, sources) = await ScopeAsync(db, i, ct);
        var hash = InputHash(i, photos, sources);
        var raw = await ai.StepAsync(
            new(
                "question",
                r.RequestId,
                hash,
                "inspect-v1",
                photos,
                sources,
                i.EquipmentModel,
                i.ManualVersion,
                r.Question
            ),
            ct
        );
        var result =
            JsonSerializer.Deserialize<QuestionResult>(raw.OutputJson, Json)
            ?? throw new AppException(502, "invalid_answer", "No AI answer.");
        if (
            string.IsNullOrWhiteSpace(result.Answer)
            || result.Answer.Length > 8000
            || result.Evidence is null
            || result.PhotoDocumentIds is null
            || result.PhotoDocumentIds.Any(x => !photos.Any(p => p.DocumentId == x))
        )
            throw new AppException(502, "invalid_answer", "Invalid answer schema/scope.");
        if (result.Evidence.Length > 0)
            GroundingValidator.ValidateEvidence(result.Evidence, sources);
        if (result.Evidence.Length == 0)
            result = result with
            {
                Answer = "Informația nu este disponibilă în manualul selectat.",
                PhotoDocumentIds = [],
            };
        // Revision token rejects source deletion/edits that occurred during the external call.
        i.Revision++;
        db.Add(
            new QuestionMessage
            {
                InspectionId = id,
                RequestId = r.RequestId,
                Question = r.Question,
                AnswerJson = JsonSerializer.Serialize(result, Json),
                InputTokens = raw.InputTokens,
                OutputTokens = raw.OutputTokens,
            }
        );
        await SaveAsync(db, ct);
        return result;
    }

    public static async Task<Inspection> GetEntityAsync(
        AppDbContext db,
        Guid id,
        CancellationToken ct
    ) =>
        await db.Inspections.SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new AppException(404, "inspection_not_found", "Inspection not found.");

    public static void CheckRevision(Inspection i, int revision)
    {
        if (i.Revision != revision)
            throw new AppException(
                409,
                "revision_conflict",
                "Reload the inspection; it changed since this request."
            );
    }

    public static async Task<(PhotoSource[] Photos, ManualSource[] Sources)> ScopeAsync(
        AppDbContext db,
        Inspection i,
        CancellationToken ct
    )
    {
        var links = await db
            .InspectionSources.Where(x => x.InspectionId == i.Id)
            .OrderBy(x => x.DocumentId)
            .ToArrayAsync(ct);
        if (!links.Any(x => x.Role == "photo") || links.Count(x => x.Role == "manual") != 1)
            throw new AppException(
                409,
                "missing_sources",
                "Select at least one photograph and one matching manual."
            );
        var ids = links.Select(x => x.DocumentId).ToArray();
        var docs = await db.Documents.Where(x => ids.Contains(x.Id)).ToArrayAsync(ct);
        if (docs.Any(x => x.Status != "Ready") || docs.Length != ids.Length)
            throw new AppException(
                409,
                "sources_not_ready",
                "All sources must be extracted and active."
            );
        var photos = links
            .Where(x => x.Role == "photo")
            .Select(x =>
            {
                var d = docs.Single(d => d.Id == x.DocumentId);
                return new PhotoSource(
                    d.Id,
                    d.PhotoObjectKey ?? d.ObjectKey,
                    d.PhotoObjectKey is null ? d.ContentType : "image/png",
                    d.Sha256
                );
            })
            .ToArray();
        var manual = docs.Single(x => x.Id == links.Single(s => s.Role == "manual").DocumentId);
        var chunks = await db
            .DocumentChunks.Where(x =>
                x.DocumentId == manual.Id && x.Generation == manual.ActiveGeneration
            )
            .OrderBy(x => x.Ordinal)
            .ToArrayAsync(ct);
        if (chunks.Length == 0 || chunks.Length > 200 || chunks.Sum(x => x.Text.Length) > 100000)
            throw new AppException(
                409,
                "manual_context_limit",
                "Manual context must contain 1–200 chunks and at most 100000 characters."
            );
        return (
            photos,
            chunks
                .Select(x => new ManualSource(
                    x.Id,
                    x.DocumentId,
                    x.Generation,
                    x.Text,
                    x.PageNumber,
                    manual.Sha256
                ))
                .ToArray()
        );
    }

    public static string InputHash(Inspection i, PhotoSource[] p, ManualSource[] s) =>
        Hash(
            JsonSerializer.Serialize(
                new
                {
                    i.EquipmentModel,
                    i.ManualVersion,
                    Photos = p,
                    Sources = s,
                },
                Json
            )
        );

    private static async Task SaveAsync(AppDbContext db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AppException(409, "revision_conflict", "Reload changed inspection.");
        }
        catch (DbUpdateException e)
            when (e.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            throw new AppException(
                409,
                "duplicate_request",
                "This source/request already exists; reload."
            );
        }
    }
}
