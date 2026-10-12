using MeetingAI.Api.Services;
using MeetingAI.Domain.DTOs;
using Xunit;
namespace MeetingAI.Tests;

public sealed class SegmentValidatorTests
{
    [Fact] public void AcceptsValidTimestampsAndOverlapBetweenSpeakers() { var first = Guid.NewGuid(); var second = Guid.NewGuid(); SegmentValidator.Validate([new(first, 0, 0, 100, "speaker-1", "speaker-1", "hello"), new(second, 1, 50, 150, "speaker-2", "speaker-2", "world")], 200); }
    [Theory][InlineData(-1, 10)][InlineData(10, 10)][InlineData(0, 201)] public void RejectsInvalidRanges(long start, long end) { Assert.Throws<ArgumentException>(() => SegmentValidator.Validate([new(Guid.NewGuid(), 0, start, end, null, null, "valid")], 200)); }
    [Fact] public void RejectsDuplicateIds() { var id = Guid.NewGuid(); Assert.Throws<ArgumentException>(() => SegmentValidator.Validate([new(id, 0, 0, 10, null, null, "a"), new(id, 1, 10, 20, null, null, "b")], 100)); }
    [Fact] public void RejectsEmptyText() { Assert.Throws<ArgumentException>(() => SegmentValidator.Validate([new(Guid.NewGuid(), 0, 0, 10, null, null, " ")], 100)); }
}
