using Microsoft.EntityFrameworkCore;
using SupportAI.Application.Abstractions;
using SupportAI.Database.Persistence;
using SupportAI.Domain.DTOs;
using SupportAI.Domain.Entities;
using SupportAI.Domain.Models;
using System.Text.Json;
namespace SupportAI.Application.Services;

public sealed class ActionService(IDbContextFactory<AppDbContext> factory, IOrderReader orders)
{
    public async Task<ActionExecution> ConfirmAsync(Guid id, ConfirmAction request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 128)
            throw new SupportException("invalid_idempotency_key");
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // A row lock serializes confirmation and rejection without any provider call in the transaction.
        var proposal = await db.ActionProposals.FromSqlInterpolated($"SELECT * FROM app.\"ActionProposals\" WHERE \"Id\"={id} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw new SupportException("proposal_not_found", 404);
        var existing = await db.ActionExecutions.SingleOrDefaultAsync(x => x.ProposalId == id, ct);
        if (existing is not null)
        {
            if (existing.IdempotencyKey != request.IdempotencyKey || proposal.PayloadHash != request.PayloadHash)
                throw new SupportException("idempotency_conflict", 409);
            return existing;
        }
        if (proposal.Revision != request.Revision || proposal.PayloadHash != request.PayloadHash || proposal.State != "Draft" || proposal.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            throw new SupportException("proposal_conflict", 409);
        if (await db.ActionExecutions.AnyAsync(x => x.IdempotencyKey == request.IdempotencyKey, ct))
            throw new SupportException("idempotency_conflict", 409);
        var item = await db.SupportCases.SingleAsync(x => x.Id == proposal.CaseId, ct);
        if (proposal.Kind == "return" && orders.Find(item.OrderReference)?.ReturnEligible != true)
            throw new SupportException("return_ineligible", 409);
        var result = new ActionExecution { ProposalId = id, IdempotencyKey = request.IdempotencyKey, ResultJson = JsonSerializer.Serialize(new { localId = Guid.NewGuid(), kind = proposal.Kind, scope = "local-demo", arguments = JsonSerializer.Deserialize<JsonElement>(proposal.ArgumentsJson) }) };
        proposal.State = "Executed";
        proposal.Revision++;
        item.State = "Closed";
        item.Revision++;
        db.Add(result);
        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: "23505" }) { throw new SupportException("idempotency_conflict", 409); }
        return result;
    }
    public async Task RejectAsync(Guid id, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var proposal = await db.ActionProposals.FromSqlInterpolated($"SELECT * FROM app.\"ActionProposals\" WHERE \"Id\"={id} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw new SupportException("proposal_not_found", 404);
        if (proposal.State != "Draft")
            throw new SupportException("proposal_conflict", 409);
        proposal.State = "Rejected";
        proposal.Revision++;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
