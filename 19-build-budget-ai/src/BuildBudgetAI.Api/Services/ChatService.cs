using System.Text.Json;
using BuildBudgetAI.Database;
using BuildBudgetAI.Domain.Dto;
using BuildBudgetAI.Domain.Entities;
using BuildBudgetAI.Domain.Interfaces;
using BuildBudgetAI.Domain.Models;
using BuildBudgetAI.Services;
using Microsoft.EntityFrameworkCore;

namespace BuildBudgetAI.Api.Services;

public sealed class ChatService(AppDbContext db, PlanService plans, IAiClient ai)
{
    public async Task<ChatResult> AskAsync(ChatRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Question) || r.Question.Length > 2000)
            throw new DomainException("invalid_question");
        var p = await plans.LoadAsync(r.PlanId, ct);
        long revision = p.Revision;
        var chunks = await db
            .Chunks.Where(x =>
                db.Sources.Any(s => s.PlanId == p.Id && s.DocumentId == x.DocumentId)
                && db.Documents.Any(d =>
                    d.Id == x.DocumentId
                    && d.Status == "Ready"
                    && d.ActiveGeneration == x.Generation
                )
            )
            .OrderBy(x => x.DocumentId)
            .ThenBy(x => x.Ordinal)
            .Take(8)
            .ToListAsync(ct);
        var evidence = chunks
            .Select(x => new EvidenceDto(x.Id.ToString(), x.DocumentId, x.PageNumber, x.Text))
            .ToArray();
        var budget = await plans.LatestAsync(p.Id, p.Revision, ct);
        var result = await ai.GenerateAsync(
            new(
                r.Question,
                JsonSerializer.Serialize(
                    new
                    {
                        name = p.Name,
                        revision,
                        budget,
                        needs = p.Needs.Select(x => new
                        {
                            x.Label,
                            x.Unit,
                            quantity = BudgetCalculator.Number(x.Quantity),
                        }),
                    }
                ),
                evidence
            ),
            ct
        );
        if (
            result.CitationLabels.Distinct().Count() != result.CitationLabels.Count
            || result.CitationLabels.Any(x => !evidence.Any(e => e.SourceId == x))
        )
            throw new DomainException("invalid_citations", 502);
        if (
            await db.Plans.Where(x => x.Id == p.Id).Select(x => x.Revision).SingleAsync(ct)
            != revision
        )
            throw new DomainException("analysis_stale", 409);
        foreach (var chunk in chunks)
            if (
                !await db.Documents.AnyAsync(
                    x =>
                        x.Id == chunk.DocumentId
                        && x.Status == "Ready"
                        && x.ActiveGeneration == chunk.Generation,
                    ct
                )
            )
                throw new DomainException("source_stale", 409);
        var conversation = r.ConversationId.HasValue
            ? await db.Conversations.SingleOrDefaultAsync(
                x => x.Id == r.ConversationId && x.PlanId == p.Id,
                ct
            ) ?? throw new DomainException("conversation_not_found", 404)
            : new Conversation { PlanId = p.Id };
        if (!r.ConversationId.HasValue)
            db.Conversations.Add(conversation);
        db.Messages.Add(
            new Message
            {
                ConversationId = conversation.Id,
                Role = "user",
                Text = r.Question,
            }
        );
        var message = new Message
        {
            ConversationId = conversation.Id,
            Text = result.Summary,
            Deployment = result.Deployment,
            InputTokens = result.InputTokens,
            OutputTokens = result.OutputTokens,
        };
        db.Messages.Add(message);
        var cited = evidence.Where(x => result.CitationLabels.Contains(x.SourceId)).ToArray();
        foreach (var e in cited)
            db.Citations.Add(
                new Citation
                {
                    MessageId = message.Id,
                    DocumentId = e.DocumentId,
                    ChunkId = Guid.Parse(e.SourceId),
                    Generation = chunks.Single(x => x.Id.ToString() == e.SourceId).Generation,
                    Label = e.SourceId,
                    Excerpt = e.Text,
                    PageNumber = e.PageNumber,
                }
            );
        await db.SaveChangesAsync(ct);
        return new(conversation.Id, result.Summary, result.Unknowns, cited, result.Deployment);
    }
}
