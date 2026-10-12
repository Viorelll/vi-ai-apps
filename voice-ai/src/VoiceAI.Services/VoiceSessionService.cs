using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using VoiceAI.Database.Persistence;
using VoiceAI.Domain.DTOs;
using VoiceAI.Domain.Entities;
using VoiceAI.Domain.Services;

namespace VoiceAI.Services;

public sealed class VoiceSessionService(AppDbContext db, HttpClient aiClient)
{
    public async Task<VoiceSession> CreateAsync(
        string timeZoneId,
        CancellationToken cancellationToken
    )
    {
        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            throw new InvalidOperationException("timezone_not_supported");
        }
        var session = new VoiceSession
        {
            Id = Guid.NewGuid(),
            TimeZoneId = zone.Id,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        db.VoiceSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task<(VoiceTurn Turn, Guid? ProposalId)> SubmitTextAsync(
        Guid sessionId,
        TurnRequest request,
        CancellationToken ct
    )
    {
        if (
            string.IsNullOrWhiteSpace(request.RequestId)
            || request.RequestId.Length > 100
            || string.IsNullOrWhiteSpace(request.Text)
            || request.Text.Length > 4000
        )
            throw new InvalidOperationException("invalid_turn");
        var previous = await db.VoiceTurns.FirstOrDefaultAsync(
            x => x.SessionId == sessionId && x.RequestId == request.RequestId,
            ct
        );
        if (previous is not null)
            return (previous, await ProposalForTurnAsync(sessionId, previous.Id, ct));
        var session =
            await db.VoiceSessions.SingleOrDefaultAsync(x => x.Id == sessionId, ct)
            ?? throw new KeyNotFoundException("session_not_found");
        var now = DateTimeOffset.UtcNow;
        var slots = await db
            .AvailabilitySlots.Where(x => x.StartUtc > now && x.Booking == null)
            .OrderBy(x => x.StartUtc)
            .Take(30)
            .ToListAsync(ct);
        var response = await aiClient.PostAsJsonAsync(
            "internal/voice/turn",
            new AiTurnRequest(
                request.Text,
                session.TimeZoneId,
                slots
                    .Select(x => new AvailableSlotDto(x.Id, x.StartUtc, x.EndUtc, x.ResourceLabel))
                    .ToArray()
            ),
            ct
        );
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable
                    ? "ai_not_configured"
                    : "ai_request_failed"
            );
        var result =
            await response.Content.ReadFromJsonAsync<AiTurnResult>(ct)
            ?? throw new InvalidOperationException("invalid_ai_response");
        var turn = new VoiceTurn
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            RequestId = request.RequestId,
            Transcript = request.Text.Trim(),
            Reply = result.Reply,
            CreatedAtUtc = now,
        };
        Guid? proposalId = null;
        if (result.SlotId is Guid slotId)
        {
            var slot =
                slots.SingleOrDefault(x => x.Id == slotId)
                ?? throw new InvalidOperationException("ai_returned_unknown_slot");
            var proposal = new BookingProposal
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                VoiceTurnId = turn.Id,
                SlotId = slot.Id,
                SlotRevision = slot.Revision,
                ExpiresAtUtc = now.AddMinutes(5),
            };
            db.BookingProposals.Add(proposal);
            proposalId = proposal.Id;
        }
        db.VoiceTurns.Add(turn);
        session.Revision++;
        await db.SaveChangesAsync(ct);
        return (turn, proposalId);
    }

    private async Task<Guid?> ProposalForTurnAsync(
        Guid sessionId,
        Guid turnId,
        CancellationToken ct
    ) =>
        await db
            .BookingProposals.Where(x => x.SessionId == sessionId && x.VoiceTurnId == turnId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);

    public async Task<object> ConfirmAsync(
        Guid proposalId,
        ConfirmProposalRequest request,
        CancellationToken ct
    )
    {
        if (
            string.IsNullOrWhiteSpace(request.IdempotencyKey)
            || request.IdempotencyKey.Length > 100
        )
            throw new InvalidOperationException("invalid_idempotency_key");
        var prior = await db
            .LocalBookings.Include(x => x.Proposal)
            .FirstOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey, ct);
        if (prior is not null)
            return new
            {
                bookingId = prior.Id,
                slotId = prior.SlotId,
                confirmedAtUtc = prior.ConfirmedAtUtc,
            };
        prior = await db.LocalBookings.FirstOrDefaultAsync(x => x.ProposalId == proposalId, ct);
        if (prior is not null)
            return new
            {
                bookingId = prior.Id,
                slotId = prior.SlotId,
                confirmedAtUtc = prior.ConfirmedAtUtc,
            };
        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            ct
        );
        var proposal =
            await db
                .BookingProposals.Include(x => x.Slot)
                .SingleOrDefaultAsync(x => x.Id == proposalId, ct)
            ?? throw new KeyNotFoundException("proposal_not_found");
        ProposalRules.ValidateConfirmation(proposal, request.Revision, DateTimeOffset.UtcNow);
        if (proposal.Slot is null || proposal.Slot.Revision != proposal.SlotRevision)
            throw new InvalidOperationException("slot_changed");
        if (await db.LocalBookings.AnyAsync(x => x.SlotId == proposal.SlotId, ct))
            throw new InvalidOperationException("slot_unavailable");
        var booking = new LocalBooking
        {
            Id = Guid.NewGuid(),
            ProposalId = proposal.Id,
            SlotId = proposal.SlotId,
            IdempotencyKey = request.IdempotencyKey,
            ConfirmedAtUtc = DateTimeOffset.UtcNow,
        };
        proposal.State = "Confirmed";
        db.LocalBookings.Add(booking);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new
        {
            bookingId = booking.Id,
            slotId = booking.SlotId,
            confirmedAtUtc = booking.ConfirmedAtUtc,
        };
    }
}
