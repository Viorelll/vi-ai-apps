using Microsoft.EntityFrameworkCore;
using TutorAI.Database.Persistence;
using TutorAI.Domain.DTOs;
using TutorAI.Domain.Entities;
using TutorAI.Domain.Models;
using TutorAI.Domain.Rules;

namespace TutorAI.Services.Lessons;

public sealed class LessonService(IDbContextFactory<AppDbContext> factory)
{
    public async Task<SourceEvidence[]> SourcesAsync(string? topic, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db
            .LearningSources.Where(x => x.Active && (topic == null || x.Objective == topic))
            .Select(x => new SourceEvidence(
                x.Id,
                x.Title,
                x.Objective,
                x.Text,
                x.OfficialUrl,
                x.CheckedAtUtc
            ))
            .Take(50)
            .ToArrayAsync(ct);
    }

    public async Task<Guid> AddAsync(AddSourceRequest r, CancellationToken ct)
    {
        if (
            !OfficialSourcePolicy.IsAllowed(r.OfficialUrl)
            || string.IsNullOrWhiteSpace(r.Objective)
            || r.Objective.Length > 200
            || string.IsNullOrWhiteSpace(r.Title)
            || r.Title.Length > 300
        )
            throw new AppException(
                400,
                "invalid_official_source",
                "Use a dated Microsoft Learn source and bounded title/objective."
            );
        await using var db = await factory.CreateDbContextAsync(ct);
        var d =
            await db.Documents.FindAsync([r.DocumentId], ct)
            ?? throw new AppException(404, "document_not_found", "Document not found.");
        if (d.Status != "Ready")
            throw new AppException(409, "document_not_ready", "Wait for extraction.");
        var chunks = await db
            .DocumentChunks.Where(x => x.DocumentId == d.Id && x.Generation == d.ActiveGeneration)
            .OrderBy(x => x.Ordinal)
            .Select(x => x.Text)
            .ToArrayAsync(ct);
        var text = string.Join("\n", chunks);
        if (text.Length > 50000)
            throw new AppException(
                413,
                "source_context_limit",
                "Split this learning source into smaller documents."
            );
        var s = new LearningSource
        {
            DocumentId = d.Id,
            OfficialUrl = r.OfficialUrl,
            Objective = r.Objective,
            Title = r.Title,
            Text = text,
            CheckedAtUtc = DateTimeOffset.UtcNow,
        };
        db.Add(s);
        await db.SaveChangesAsync(ct);
        return s.Id;
    }
}
