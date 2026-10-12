using VideoSearchAI.Ai.Providers;
using VideoSearchAI.Ai.Providers.Extraction;

namespace VideoSearchAI.Tests;

public sealed class VideoUnderstandingMapperTests
{
    [Fact]
    public void MapsWebVttAndKeepsVisualProvenanceSeparate()
    {
        var raw =
            """{"result":{"contents":[{"startTimeMs":10000,"endTimeMs":35000,"markdown":"# Video: 00:10.000 => 00:35.000\nAn index diagram.\n\nTranscript\nWEBVTT\n\n00:10.000 --> 00:35.000\n<Speaker 1>CREATE INDEX\n\nKey Frames"}]}}""";
        var evidence = new VideoUnderstandingMapper().Map(raw, 60000, 180000);
        Assert.Equal(2, evidence.Length);
        var verbal = Assert.Single(evidence, x => x.Transcript.Length > 0);
        Assert.Equal(70000, verbal.StartMs);
        Assert.Equal(95000, verbal.EndMs);
        Assert.Equal("CREATE INDEX", verbal.Transcript);
        Assert.Null(verbal.VisualText);
        var visual = Assert.Single(evidence, x => x.VisualText != null);
        Assert.Empty(visual.Transcript);
    }

    [Fact]
    public void RejectsDocumentOnlySchema() =>
        Assert.Throws<ProviderException>(() =>
            new VideoUnderstandingMapper().Map("{\"result\":{\"pages\":[]}}", 0, 180000)
        );

    [Fact]
    public void RejectsOutOfVideoEvidence() =>
        Assert.Throws<ProviderException>(() =>
            new VideoUnderstandingMapper().Map(
                """{"result":{"contents":[{"startTimeMs":70000,"endTimeMs":195000,"markdown":"An index"}]}}""",
                0,
                180000
            )
        );
}
