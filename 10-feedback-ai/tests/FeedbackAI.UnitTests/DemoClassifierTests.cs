using Xunit;

namespace FeedbackAI.UnitTests;

public sealed class DemoClassifierTests
{
    [Fact]
    public void SeparatesDeliveryAndInformationTopicsAndKeepsNegativeUrgencyNormal()
    {
        var delivery = DemoClassifier.Classify("Comanda întârzie de trei zile.");
        var information = DemoClassifier.Classify("Îmi puteți trimite informații despre garanție?");

        Assert.Equal("delivery", delivery.Topic);
        Assert.Equal("negative", delivery.Sentiment);
        Assert.Equal("normal", delivery.Urgency);
        Assert.Equal("information", information.Topic);
    }
}
