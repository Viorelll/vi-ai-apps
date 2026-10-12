using VoiceAI.Domain.Entities;

namespace VoiceAI.Domain.Services;

public static class ProposalRules
{
    public static void ValidateConfirmation(
        BookingProposal proposal,
        long expectedRevision,
        DateTimeOffset now
    )
    {
        if (proposal.State != "Draft")
            throw new InvalidOperationException("proposal_not_draft");
        if (proposal.Revision != expectedRevision)
            throw new InvalidOperationException("proposal_revision_conflict");
        if (proposal.ExpiresAtUtc <= now)
            throw new InvalidOperationException("proposal_expired");
    }
}
