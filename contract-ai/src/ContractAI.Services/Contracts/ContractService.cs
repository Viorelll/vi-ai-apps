using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ContractAI.Database.Persistence;
using ContractAI.Domain.DTOs;
using ContractAI.Domain.Entities;
using ContractAI.Domain.Models;
using ContractAI.Domain.Services;
using ContractAI.Infrastructure.Abstractions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ContractAI.Services.Contracts;

public sealed class ContractService(IDbContextFactory<AppDbContext> factory, IAiClient ai)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ContractVersion> CreateAsync(CreateVersionRequest input, CancellationToken ct)
    {
        if (
            string.IsNullOrWhiteSpace(input.ContractKey)
            || input.ContractKey.Length > 100
            || string.IsNullOrWhiteSpace(input.VersionLabel)
            || input.VersionLabel.Length > 80
        )
            throw new AppException(
                400,
                "invalid_version",
                "Contract key and version label are required (100/80 characters maximum)."
            );
        await using var db = await factory.CreateDbContextAsync(ct);
        var doc =
            await db.Documents.SingleOrDefaultAsync(
                x => x.Id == input.DocumentId && x.Status != "Deleting",
                ct
            ) ?? throw new AppException(404, "document_not_found", "Document not found.");
        var key = input.ContractKey.Trim();
        if (
            input.ParentVersionId is { } parent
            && !await db.ContractVersions.AnyAsync(
                x => x.Id == parent && x.ContractKey == key && x.State != "Stale",
                ct
            )
        )
            throw new AppException(
                409,
                "invalid_parent",
                "Parent must belong to the same contract."
            );
        var version = new ContractVersion
        {
            DocumentId = doc.Id,
            ContractKey = key,
            VersionLabel = input.VersionLabel.Trim(),
            ParentVersionId = input.ParentVersionId,
        };
        db.Add(version);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            throw new AppException(
                409,
                "duplicate_version",
                "Version label already exists for this contract."
            );
        }
        return version;
    }

    public async Task<Guid> AnalyzeAsync(Guid id, long revision, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var v =
            await db.ContractVersions.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException(404, "version_not_found", "Version not found.");
        if (v.Revision != revision || v.State is "Analyzing" or "Stale")
            throw new AppException(409, "version_revision", "Version state or revision changed.");
        if (!await db.Documents.AnyAsync(x => x.Id == v.DocumentId && x.Status == "Ready", ct))
            throw new AppException(
                409,
                "document_not_ready",
                "Wait for document extraction or retry its failed job."
            );
        v.State = "Analyzing";
        v.Revision++;
        var job = new IngestionJob { Kind = "Analyze", TargetId = v.Id };
        db.Add(job);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AppException(409, "version_revision", "Version changed concurrently.");
        }
        return job.Id;
    }

    public async Task<object> GetAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var version =
            await db.ContractVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new AppException(404, "version_not_found", "Version not found.");
        var clauses = await db
            .Clauses.AsNoTracking()
            .Where(x => x.VersionId == id)
            .OrderBy(x => x.Ordinal)
            .ToArrayAsync(ct);
        var obligations = await db
            .Obligations.AsNoTracking()
            .Where(x => x.VersionId == id)
            .ToArrayAsync(ct);
        var findings = await db
            .Findings.AsNoTracking()
            .Where(x => x.VersionId == id)
            .ToArrayAsync(ct);
        return new
        {
            version,
            clauses,
            obligations,
            findings,
            unknowns = JsonSerializer.Deserialize<string[]>(version.UnknownsJson, Json),
            citations = (await SourcesAsync(db, [id], false, ct)).Select(s => Cite(s, s.Text)),
        };
    }

    public async Task<Comparison> CompareAsync(CompareRequest input, CancellationToken ct)
    {
        if (input.LeftVersionId == input.RightVersionId)
            throw new AppException(400, "same_version", "Select two distinct versions.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var versions = await db
            .ContractVersions.Where(x =>
                x.Id == input.LeftVersionId || x.Id == input.RightVersionId
            )
            .ToArrayAsync(ct);
        if (
            versions.Length != 2
            || versions.Any(x => x.State is not ("Ready" or "NeedsReview"))
            || versions[0].ContractKey != versions[1].ContractKey
        )
            throw new AppException(
                409,
                "comparison_scope",
                "Both versions must be analyzed and belong to the same contract."
            );
        var sources = await SourcesAsync(db, [input.LeftVersionId, input.RightVersionId], true, ct);
        var hash = Hash(JsonSerializer.Serialize(sources, Json));
        var existing = await db.Comparisons.SingleOrDefaultAsync(
            x =>
                x.LeftVersionId == input.LeftVersionId
                && x.RightVersionId == input.RightVersionId
                && x.PipelineVersion == "contract-v1"
                && x.InputHash == hash,
            ct
        );
        if (existing is not null)
            return existing;
        var comparison = new Comparison
        {
            LeftVersionId = input.LeftVersionId,
            RightVersionId = input.RightVersionId,
            InputHash = hash,
        };
        db.Add(comparison);
        db.Add(new IngestionJob { Kind = "Compare", TargetId = comparison.Id });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            db.ChangeTracker.Clear();
            return await db.Comparisons.SingleAsync(
                x =>
                    x.LeftVersionId == input.LeftVersionId
                    && x.RightVersionId == input.RightVersionId
                    && x.InputHash == hash,
                ct
            );
        }
        return comparison;
    }

    public async Task<AnswerDto> QuestionAsync(
        Guid versionId,
        QuestionRequest input,
        CancellationToken ct
    )
    {
        if (
            input.RequestId == Guid.Empty
            || string.IsNullOrWhiteSpace(input.Question)
            || input.Question.Length > 4000
        )
            throw new AppException(
                400,
                "invalid_question",
                "Request ID and a question of 1-4000 characters are required."
            );
        await using var db = await factory.CreateDbContextAsync(ct);
        var version =
            await db.ContractVersions.SingleOrDefaultAsync(
                x => x.Id == versionId && (x.State == "Ready" || x.State == "NeedsReview"),
                ct
            )
            ?? throw new AppException(
                409,
                "version_not_ready",
                "Selected version must be analyzed and active."
            );
        Conversation conversation;
        if (input.ConversationId is { } conversationId)
            conversation =
                await db.Conversations.SingleOrDefaultAsync(
                    x => x.Id == conversationId && x.VersionId == versionId,
                    ct
                )
                ?? throw new AppException(
                    409,
                    "conversation_scope",
                    "Conversation belongs to another version."
                );
        else
        {
            conversation = new Conversation { VersionId = versionId };
            db.Add(conversation);
        }
        var existing = await db
            .Messages.AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.VersionId == versionId && x.RequestId == input.RequestId,
                ct
            );
        var sources = await SourcesAsync(db, [versionId], true, ct);
        if (existing is not null)
        {
            if (
                existing.Question != input.Question
                || (input.ConversationId is { } known && known != existing.ConversationId)
            )
                throw new AppException(
                    409,
                    "request_id_conflict",
                    "Request ID was already used for different input."
                );
            return await AnswerAsync(db, existing, sources, ct);
        }
        var history = await db
            .Messages.AsNoTracking()
            .Where(x => x.ConversationId == conversation.Id)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(4)
            .Select(x => new QuestionHistory(x.Question, x.Answer))
            .ToArrayAsync(ct);
        Array.Reverse(history);
        var result = await ai.StepAsync(
            new(
                "question",
                input.RequestId,
                Hash(JsonSerializer.Serialize(sources, Json)),
                "contract-v1",
                sources,
                input.Question,
                History: history
            ),
            ct
        );
        var answer =
            JsonSerializer.Deserialize<QuestionResult>(result.OutputJson, Json)
            ?? throw new AppException(502, "invalid_question_result", "Invalid question response.");
        if (string.IsNullOrWhiteSpace(answer.Answer) || answer.Answer.Length > 12000)
            throw new AppException(502, "invalid_answer", "Invalid answer length.");
        GroundingValidator.Validate(answer.Evidence, sources, false);
        // Revalidate immediately before commit: deletion must remove sources from subsequent answers.
        var activeSources = await SourcesAsync(db, [versionId], true, ct);
        GroundingValidator.Validate(answer.Evidence, activeSources, false);
        if (answer.Evidence.Length == 0)
            answer = answer with
            {
                Answer = "Informația nu este disponibilă în versiunea selectată.",
            };
        var message = new Message
        {
            VersionId = versionId,
            ConversationId = conversation.Id,
            RequestId = input.RequestId,
            Question = input.Question,
            Answer = answer.Answer,
            Deployment = result.Deployment,
            InputTokens = result.InputTokens,
            OutputTokens = result.OutputTokens,
        };
        db.Add(message);
        foreach (var e in answer.Evidence)
        {
            var s = sources.Single(x => x.Id == e.ClauseId);
            db.Add(
                new Citation
                {
                    MessageId = message.Id,
                    DocumentId = s.DocumentId,
                    ChunkId = s.ChunkId,
                    VersionId = s.VersionId,
                    Generation = s.Generation,
                    PageNumber = s.PageNumber,
                    Excerpt = e.Quote,
                }
            );
        }
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            db.ChangeTracker.Clear();
            message = await db.Messages.SingleAsync(
                x => x.VersionId == versionId && x.RequestId == input.RequestId,
                ct
            );
        }
        return await AnswerAsync(db, message, sources, ct);
    }

    private static async Task<AnswerDto> AnswerAsync(
        AppDbContext db,
        Message message,
        ClauseSource[] sources,
        CancellationToken ct
    )
    {
        var citations = await db.Citations.Where(x => x.MessageId == message.Id).ToArrayAsync(ct);
        return new(
            message.ConversationId,
            message.Id,
            message.Answer,
            citations
                .Select(x => new CitationDto(
                    sources.FirstOrDefault(s => s.ChunkId == x.ChunkId)?.Id ?? Guid.Empty,
                    x.VersionId,
                    x.DocumentId ?? Guid.Empty,
                    x.Generation,
                    x.PageNumber,
                    x.Excerpt,
                    ContentUrl(x.DocumentId ?? Guid.Empty, x.PageNumber)
                ))
                .ToArray(),
            message.Deployment,
            message.InputTokens,
            message.OutputTokens
        );
    }

    public static async Task<ClauseSource[]> SourcesAsync(
        AppDbContext db,
        Guid[] ids,
        bool requireActive,
        CancellationToken ct
    )
    {
        var sources = await (
            from c in db.Clauses.AsNoTracking()
            join v in db.ContractVersions.AsNoTracking() on c.VersionId equals v.Id
            join d in db.Documents.AsNoTracking() on v.DocumentId equals d.Id
            join ch in db.DocumentChunks.AsNoTracking() on c.ChunkId equals ch.Id
            where
                ids.Contains(v.Id)
                && v.State != "Stale"
                && d.Status == "Ready"
                && ch.Generation == d.ActiveGeneration
            select new ClauseSource(
                c.Id,
                v.Id,
                d.Id,
                c.ChunkId,
                ch.Generation,
                c.PageNumber,
                c.Text,
                c.Ordinal
            )
        ).ToArrayAsync(ct);
        if (
            requireActive
            && (sources.Length == 0 || ids.Any(id => !sources.Any(x => x.VersionId == id)))
        )
            throw new AppException(
                409,
                "stale_sources",
                "Source document was deleted or is unavailable."
            );
        return sources.OrderBy(s => s.VersionId).ThenBy(s => s.Ordinal).ThenBy(s => s.Id).ToArray();
    }

    public static string Hash(string input) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));

    public static CitationDto Cite(ClauseSource source, string quote) =>
        new(
            source.Id,
            source.VersionId,
            source.DocumentId,
            source.Generation,
            source.PageNumber,
            quote,
            ContentUrl(source.DocumentId, source.PageNumber)
        );

    private static string ContentUrl(Guid id, int? page) =>
        $"/api/documents/{id}/content" + (page is null ? "" : $"#page={page}");
}
