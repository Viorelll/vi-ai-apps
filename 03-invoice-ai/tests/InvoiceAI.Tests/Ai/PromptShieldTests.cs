using Microsoft.Extensions.Options;
using InvoiceAI.Ai.Configuration;
using InvoiceAI.Ai.Providers.Safety;
using InvoiceAI.Infrastructure.Ai;
using InvoiceAI.Tests.Fixtures;
namespace InvoiceAI.Tests.Ai;

public sealed class PromptShieldTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task BlocksPromptAndDocumentAttack(bool prompt, bool document)
    {
        var handler = new FakeHttpHandler((_, _) => System.Text.Json.JsonSerializer.Serialize(new { userPromptAnalysis = new { attackDetected = prompt }, documentsAnalysis = new[] { new { attackDetected = document } } }));
        var client = new PromptShieldClient(new(handler), Options.Create(new SafetyOptions { PromptShieldsEnabled = true, Endpoint = "https://safety.cognitiveservices.azure.com/", ApiKey = "test-key" }));
        Assert.Equal("PROMPT_ATTACK_DETECTED", (await Assert.ThrowsAsync<GatewayException>(() => client.CheckAsync("question", ["untrusted text"], default))).Code);
    }
    [Fact]
    public async Task DisabledDoesNotCallCloud()
    {
        var h = new FakeHttpHandler((_, _) => throw new InvalidOperationException());
        var c = new PromptShieldClient(new(h), Options.Create(new SafetyOptions()));
        await c.CheckAsync("question", [], default);
        Assert.Equal(0, h.Calls);
    }
}
