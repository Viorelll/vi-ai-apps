using VideoSearchAI.Domain.Models;
using VideoSearchAI.Domain.Services;

namespace VideoSearchAI.Tests;

public sealed class TemporalRulesTests
{
    [Fact]
    public void FragmentOffsetUsesOriginalTimeline() =>
        Assert.Equal((70000L, 95000L), TemporalRules.ToOriginalRange(10000, 35000, 60000, 180000));

    [Theory]
    [InlineData(-1, 200, 0, 180000)]
    [InlineData(100, 100, 0, 180000)]
    [InlineData(170000, 190000, 0, 180000)]
    [InlineData(1, 2, long.MaxValue, 180000)]
    public void RejectsInvalidOrOverflowRanges(long start, long end, long offset, long duration) =>
        Assert.Throws<DomainException>(() =>
            TemporalRules.ToOriginalRange(start, end, offset, duration)
        );

    [Fact]
    public void SegmentIdentityIsStableAndGenerationSpecific()
    {
        var video = Guid.NewGuid();
        Assert.Equal(TemporalRules.SegmentId(video, 1, 0), TemporalRules.SegmentId(video, 1, 0));
        Assert.NotEqual(TemporalRules.SegmentId(video, 1, 0), TemporalRules.SegmentId(video, 2, 0));
    }
}
