using VideoSearchAI.Contracts.Dto;
using VideoSearchAI.Domain.Models;
using VideoSearchAI.Services.Services;

namespace VideoSearchAI.Tests;

public sealed class CitationValidatorTests
{
    private readonly SegmentDto segment = new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        1,
        0,
        70000,
        95000,
        "CREATE INDEX",
        null,
        "transcript:1"
    );

    [Fact]
    public void ValidCitationUsesServerTimestampAndUrl()
    {
        var answer = new GeneratedAnswer(
            "Index",
            [new("S1", segment.VideoId, segment.Id, "transcript")],
            2,
            3,
            "test"
        );
        var c = Assert.Single(
            CitationValidator.Validate(
                answer,
                [new("S1", segment)],
                new Dictionary<Guid, long> { { segment.VideoId, 180000 } }
            )
        );
        Assert.Equal(70000, c.StartMs);
        Assert.Equal($"/api/video/library/{segment.VideoId}/content", c.ContentUrl);
    }

    [Fact]
    public void FabricatedSourceIsRejected() =>
        Assert.Throws<DomainException>(() =>
            CitationValidator.Validate(
                new(
                    "answer",
                    [new("S1", segment.VideoId, Guid.NewGuid(), "transcript")],
                    0,
                    0,
                    "test"
                ),
                [new("S1", segment)],
                new Dictionary<Guid, long> { { segment.VideoId, 180000 } }
            )
        );

    [Fact]
    public void VisualClaimRequiresVisualEvidence() =>
        Assert.Throws<DomainException>(() =>
            CitationValidator.Validate(
                new("answer", [new("S1", segment.VideoId, segment.Id, "visual")], 0, 0, "test"),
                [new("S1", segment)],
                new Dictionary<Guid, long> { { segment.VideoId, 180000 } }
            )
        );
}
