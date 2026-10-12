using SupportAI.Application.Services;
using SupportAI.Domain.DTOs;
using SupportAI.Domain.Models;
using Xunit;
namespace SupportAI.UnitTests;

public sealed class AnswerValidatorTests
{
    private static DomainStepRequest Request() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "status", new SeedOrderReader().Find("LAB-1001"), [new("S1", Guid.NewGuid(), Guid.NewGuid(), 1, "policy.md", "Policy", null, null, null)], []);
    [Fact] public void AllowsGroundedAnswer() => AnswerValidator.Validate(new("Delivered", "answered", ["S1"], null), Request());
    [Fact] public void RejectsInventedCitation() => Assert.Throws<SupportException>(() => AnswerValidator.Validate(new("Delivered", "answered", ["S99"], null), Request()));
    [Fact]
    public void RejectsReturnForIneligibleOrder()
    {
        var request = Request() with
        {
            Order = new SeedOrderReader().Find("LAB-1002")
        };
        Assert.Throws<SupportException>(() => AnswerValidator.Validate(new("return", "action_proposed", ["S1"], new("return", "Return", ["S1"])), request));
    }
    [Fact] public void RejectsUnknownAction() => Assert.Throws<SupportException>(() => AnswerValidator.Validate(new("refund", "action_proposed", ["S1"], new("refund", "Refund", [])), Request()));
    [Fact] public void RejectsUncitedEvidence() => Assert.Throws<SupportException>(() => AnswerValidator.Validate(new("status", "answered", [], null), Request()));
    [Fact] public void RejectsInconsistentStatus() => Assert.Throws<SupportException>(() => AnswerValidator.Validate(new("status", "answered", ["S1"], new("ticket", "Issue", [])), Request()));
}
