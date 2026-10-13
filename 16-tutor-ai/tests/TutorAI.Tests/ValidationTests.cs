using TutorAI.Domain.DTOs;
using TutorAI.Domain.Models;
using TutorAI.Services.Tutoring;
using Xunit;

namespace TutorAI.Tests;

public sealed class ValidationTests
{
    private static readonly Guid Id = Guid.NewGuid();
    private static AiRequest Request =>
        new(
            Guid.NewGuid(),
            "explain",
            "RAG",
            1,
            "q",
            [
                new(
                    Id,
                    "Title",
                    "RAG",
                    "Official text.",
                    "https://learn.microsoft.com/",
                    DateTimeOffset.UtcNow
                ),
            ]
        );

    [Fact]
    public void AcceptsExactSourceQuote() =>
        OutputValidator.Validate(
            new("Explanation", null, null, [], [new(Id, "Official text.")]),
            Request
        );

    [Fact]
    public void RejectsInventedSource() =>
        Assert.Throws<AppException>(() =>
            OutputValidator.Validate(
                new("Explanation", null, null, [], [new(Guid.NewGuid(), "Official text.")]),
                Request
            )
        );

    [Fact]
    public void RejectsFabricatedQuote() =>
        Assert.Throws<AppException>(() =>
            OutputValidator.Validate(
                new("Explanation", null, null, [], [new(Id, "Invented")]),
                Request
            )
        );

    [Fact]
    public void RejectsNoEvidence() =>
        Assert.Throws<AppException>(() =>
            OutputValidator.Validate(new("Explanation", null, null, [], []), Request)
        );

    [Fact]
    public void RejectsAnswerOutsideOptions() =>
        Assert.Throws<AppException>(() =>
            OutputValidator.Validate(
                new(
                    "Explanation",
                    new("single_choice", "Question", ["A", "B"], new("C", ["C"])),
                    null,
                    [],
                    [new(Id, "Official text.")]
                ),
                Request with
                {
                    Step = "exercise",
                    Question = "single_choice",
                }
            )
        );
}
