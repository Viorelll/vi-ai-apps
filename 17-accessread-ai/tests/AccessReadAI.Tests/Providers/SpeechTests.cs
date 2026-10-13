using System.Xml.Linq;
using AccessReadAI.Ai.Providers;
using AccessReadAI.Contracts;

namespace AccessReadAI.Tests.Providers;

public sealed class SpeechTests
{
    [Fact]
    public void EscapesSsmlWithoutExecutingMarkup()
    {
        var text = "Nu <break/> & 10 unități.";
        var ssml = SpeechClient.BuildSsml(text, "ro", "ro-RO-AlinaNeural");
        var xml = XDocument.Parse(ssml);
        Assert.Equal(text, xml.Root!.Elements().Single().Value);
        Assert.Single(xml.Root.Elements());
        Assert.Contains("&lt;break/&gt;", ssml);
    }

    [Fact]
    public void RejectsUnknownVoice() =>
        Assert.Throws<ApiFault>(() => SpeechClient.BuildSsml("text", "ro", "evil"));

    [Fact]
    public void RejectsUnknownLanguage() =>
        Assert.Throws<ApiFault>(() => SpeechClient.BuildSsml("text", "xx", "ro-RO-AlinaNeural"));

    [Fact]
    public void RejectsLongText() =>
        Assert.Throws<ApiFault>(() =>
            SpeechClient.BuildSsml(new string('a', 3001), "ro", "ro-RO-AlinaNeural")
        );
}
