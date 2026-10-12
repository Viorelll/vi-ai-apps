using Microsoft.EntityFrameworkCore;
using Npgsql;
using SupportAI.Application.Abstractions;
using SupportAI.Database.Persistence;
using SupportAI.Domain.DTOs;
using SupportAI.Domain.Entities;
using SupportAI.Domain.Models;
namespace SupportAI.Application.Services;

public sealed class SupportService(IDbContextFactory<AppDbContext> factory, IOrderReader orders)
{
    public async Task<SupportCase> CreateAsync(CreateCase request, CancellationToken ct)
    {
        if (request.OrderReference is not null && orders.Find(request.OrderReference) is null)
            throw new SupportException("order_not_found", 404);
        await using var db = await factory.CreateDbContextAsync(ct);
        var item = new SupportCase { OrderReference = request.OrderReference };
        db.Add(item);
        db.Add(new Conversation { CaseId = item.Id });
        await db.SaveChangesAsync(ct);
        return item;
    }
    public async Task<CaseDto> GetAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var item = await db.SupportCases.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new SupportException("case_not_found", 404);
        var conversation = await db.Conversations.SingleAsync(x => x.CaseId == id, ct);
        var messages = await db.Messages.Where(x => x.ConversationId == conversation.Id).OrderBy(x => x.CreatedAtUtc).ToListAsync(ct);
        var messageIds = messages.Select(x => x.Id).ToArray();
        var citations = await db.Citations.Where(x => messageIds.Contains(x.MessageId)).ToListAsync(ct);
        var attachmentIds = await db.CaseAttachments.Where(x => x.CaseId == id).Select(x => x.DocumentId).ToArrayAsync(ct);
        var attachments = await db.Documents.Where(x => attachmentIds.Contains(x.Id) && x.Status != "Deleting").ToListAsync(ct);
        var proposals = await db.ActionProposals.Where(x => x.CaseId == id).OrderBy(x => x.ExpiresAtUtc).ToListAsync(ct);
        var proposalIds = proposals.Select(x => x.Id).ToArray();
        var executions = await db.ActionExecutions.Where(x => proposalIds.Contains(x.ProposalId)).ToListAsync(ct);
        var jobs = await db.IngestionJobs.Where(x => x.TargetId != null && messageIds.Contains(x.TargetId.Value)).ToListAsync(ct);
        return new(id, item.OrderReference, item.State, item.Revision, orders.Find(item.OrderReference), messages.Select(m => new MessageDto(m.Id, m.Role, m.Text, m.Stale, m.InputTokens, m.OutputTokens, m.DurationMs, citations.Where(c => c.MessageId == m.Id).Select(c => new CitationDto(c.Label, c.DocumentId, c.ChunkId, c.FileName, c.Excerpt, c.PageNumber, c.StartMs, c.EndMs, c.DocumentId is null ? null : $"/api/documents/{c.DocumentId}/content")).ToArray())).ToArray(), attachments, proposals, executions, jobs);
    }
    public async Task AttachAsync(Guid id, Guid documentId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var item = await db.SupportCases.FromSqlInterpolated($"SELECT * FROM app.\"SupportCases\" WHERE \"Id\"={id} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw new SupportException("case_not_found", 404);
        if (!await db.Documents.AnyAsync(x => x.Id == documentId && x.Status == "Ready" && !x.IsPolicy, ct))
            throw new SupportException("document_not_ready", 409);
        if (await db.CaseAttachments.AnyAsync(x => x.CaseId == id && x.DocumentId == documentId, ct))
            return;
        var conversation = await db.Conversations.SingleAsync(x => x.CaseId == id, ct);
        var messageIds = await db.Messages.Where(x => x.ConversationId == conversation.Id).Select(x => x.Id).ToArrayAsync(ct);
        if (await db.IngestionJobs.AnyAsync(x => x.TargetId != null && messageIds.Contains(x.TargetId.Value) && x.Status != "Complete" && x.Status != "Failed", ct))
            throw new SupportException("case_busy", 409);
        await db.Messages.Where(x => x.ConversationId == conversation.Id && x.Role == "assistant").ExecuteUpdateAsync(x => x.SetProperty(m => m.Stale, true), ct);
        var drafts = await db.ActionProposals.Where(x => x.CaseId == id && x.State == "Draft").ToListAsync(ct);
        foreach (var draft in drafts)
        {
            draft.State = "Expired";
            draft.Revision++;
        }
        item.Revision++;
        item.State = "Open";
        db.Add(new CaseAttachment { CaseId = id, DocumentId = documentId });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
    public async Task<JobResult> SendAsync(Guid id, SendMessage request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 8000 || request.RequestId == Guid.Empty)
            throw new SupportException("invalid_message");
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var item = await db.SupportCases.FromSqlInterpolated($"SELECT * FROM app.\"SupportCases\" WHERE \"Id\"={id} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw new SupportException("case_not_found", 404);
        var conversation = await db.Conversations.SingleAsync(x => x.CaseId == id, ct);
        var existing = await db.Messages.SingleOrDefaultAsync(x => x.ConversationId == conversation.Id && x.RequestId == request.RequestId, ct);
        if (existing is not null)
        {
            if (existing.Text != request.Text.Trim())
                throw new SupportException("request_id_conflict", 409);
            var job = await db.IngestionJobs.SingleAsync(x => x.TargetId == existing.Id, ct);
            return new(job.Id);
        }
        var ids = await db.Messages.Where(x => x.ConversationId == conversation.Id).Select(x => x.Id).ToArrayAsync(ct);
        if (await db.IngestionJobs.AnyAsync(x => x.TargetId != null && ids.Contains(x.TargetId.Value) && x.Status != "Complete" && x.Status != "Failed", ct))
            throw new SupportException("case_busy", 409);
        var message = new Message { ConversationId = conversation.Id, RequestId = request.RequestId, Text = request.Text.Trim() };
        var work = new IngestionJob { Kind = "Support", TargetId = message.Id };
        item.Revision++;
        item.State = "Open";
        db.Add(message);
        db.Add(work);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(work.Id);
    }
}
