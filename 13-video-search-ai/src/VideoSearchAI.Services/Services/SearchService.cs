using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VideoSearchAI.Contracts.Dto;
using VideoSearchAI.Database;
using VideoSearchAI.Domain.Entities;
using VideoSearchAI.Domain.Models;

namespace VideoSearchAI.Services.Services;

public sealed class SearchService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<Guid> CreateAsync(SearchRequest r, CancellationToken ct)
    {
        if (
            string.IsNullOrWhiteSpace(r.Question)
            || r.Question.Length > 2000
            || r.VideoIds.Length == 0
            || r.VideoIds.Length > 50
            || r.RequestId == Guid.Empty
        )
            throw new DomainException("invalid_search");
        await using var db = await factory.CreateDbContextAsync(ct);
        var scope = r.VideoIds.Distinct().Order().ToArray();
        var scopeJson = JsonSerializer.Serialize(scope);
        var existing = await db.Runs.SingleOrDefaultAsync(x => x.RequestId == r.RequestId, ct);
        if (existing != null)
        {
            if (existing.Question != r.Question.Trim() || existing.AllowedVideoIdsJson != scopeJson)
                throw new DomainException("request_id_conflict", 409);
            return existing.Id;
        }
        var count = await (
            from v in db.Videos
            join d in db.Documents on v.DocumentId equals d.Id
            where scope.Contains(v.Id) && v.State == "Ready" && !d.Deleted && d.Status == "Ready"
            select v.Id
        ).CountAsync(ct);
        if (count != scope.Length)
            throw new DomainException("sources_not_ready", 409);
        var run = new SearchRun
        {
            RequestId = r.RequestId,
            Question = r.Question.Trim(),
            AllowedVideoIdsJson = scopeJson,
        };
        run.InputHash = Convert
            .ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(run.Question + scopeJson)))
            .ToLowerInvariant();
        db.Runs.Add(run);
        db.Jobs.Add(new IngestionJob { Kind = "Search", TargetId = run.Id });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e)
            when (e.InnerException
                    is Npgsql.PostgresException
                    {
                        SqlState: Npgsql.PostgresErrorCodes.UniqueViolation
                    }
            )
        {
            db.ChangeTracker.Clear();
            existing = await db.Runs.SingleAsync(x => x.RequestId == r.RequestId, ct);
            if (existing.Question != run.Question || existing.AllowedVideoIdsJson != scopeJson)
                throw new DomainException("request_id_conflict", 409);
            return existing.Id;
        }
        return run.Id;
    }

    public async Task<SearchRunDto> GetAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var run =
            await db.Runs.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new DomainException("search_not_found", 404);
        if (run.State == "Answered")
        {
            var stale = await (
                from c in db.Citations
                join d in db.Documents on c.DocumentId equals d.Id
                where
                    c.RunId == id
                    && (d.Deleted || d.Status != "Ready" || d.ActiveGeneration != c.Generation)
                select c.Id
            ).AnyAsync(ct);
            if (stale)
            {
                run.State = "Stale";
                await db.SaveChangesAsync(ct);
            }
        }
        return new(
            run.Id,
            run.State,
            run.State == "Answered" && run.ResultJson != null
                ? JsonSerializer.Deserialize<AnswerDto>(run.ResultJson)
                : null,
            run.ErrorCode
        );
    }
}
