using VoiceAI.Domain.Entities;
using VoiceAI.Domain.Services;

namespace VoiceAI.Tests;

public sealed class ProposalRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Accepts_an_unexpired_draft_at_its_current_revision()
    {
        var proposal = new BookingProposal
        {
            State = "Draft",
            Revision = 3,
            ExpiresAtUtc = Now.AddMinutes(1),
        };
        ProposalRules.ValidateConfirmation(proposal, 3, Now);
    }

    [Fact]
    public void Rejects_a_proposal_after_its_expiration()
    {
        var proposal = new BookingProposal
        {
            State = "Draft",
            Revision = 1,
            ExpiresAtUtc = Now,
        };
        var error = Assert.Throws<InvalidOperationException>(() =>
            ProposalRules.ValidateConfirmation(proposal, 1, Now)
        );
        Assert.Equal("proposal_expired", error.Message);
    }

    [Fact]
    public void Rejects_a_stale_revision()
    {
        var proposal = new BookingProposal
        {
            State = "Draft",
            Revision = 2,
            ExpiresAtUtc = Now.AddMinutes(1),
        };
        var error = Assert.Throws<InvalidOperationException>(() =>
            ProposalRules.ValidateConfirmation(proposal, 1, Now)
        );
        Assert.Equal("proposal_revision_conflict", error.Message);
    }

    [Fact]
    public void Rejects_a_proposal_that_is_not_a_draft()
    {
        var proposal = new BookingProposal
        {
            State = "Confirmed",
            Revision = 1,
            ExpiresAtUtc = Now.AddMinutes(1),
        };
        var error = Assert.Throws<InvalidOperationException>(() =>
            ProposalRules.ValidateConfirmation(proposal, 1, Now)
        );
        Assert.Equal("proposal_not_draft", error.Message);
    }
}
