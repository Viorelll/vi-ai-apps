using AccessReadAI.Ai.Models;
using AccessReadAI.Ai.Providers;

namespace AccessReadAI.Tests.Providers;

public sealed class LayoutTests
{
    [Fact]
    public void SplitsIntoBoundedOrderedBlocks()
    {
        var text = new string('a', 5200) + "\n\nlast";
        var parts = LayoutExtractor.SplitText(text).ToArray();
        Assert.Equal(4, parts.Length);
        Assert.All(parts, x => Assert.InRange(x.Length, 1, 2500));
        Assert.Equal("last", parts.Last());
    }

    [Fact]
    public void LiveConfigurationRejectsPlaceholders() =>
        Assert.False(new ProviderOptions { Demo = false }.Valid());

    [Fact]
    public void DemoIsExplicit() => Assert.True(new ProviderOptions { Demo = true }.Valid());
}
