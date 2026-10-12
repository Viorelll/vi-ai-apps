using System.Text.Json;
using Microsoft.Extensions.Options;
using SupportAI.Ai.Models;
using SupportAI.Ai.Services;
using SupportAI.Application.Services;
using SupportAI.Domain.DTOs;
using Xunit;
namespace SupportAI.UnitTests;

public sealed class DemoEvaluationTests
{
    [Fact]
    public async Task RunsTwentyFiveDeterministicAcceptanceCases()
    {
        using var dataset = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "cases.json")));
        var options = Options.Create(new FoundryOptions { Mode = "Demo" });
        var service = new FoundrySupportService(new(new HttpClient(), options), new(), options);
        var count = 0;
        foreach (var item in dataset.RootElement.EnumerateArray())
        {
            var request = new DomainStepRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), item.GetProperty("text").GetString()!, new SeedOrderReader().Find(item.GetProperty("orderReference").GetString()), [new("S1", Guid.NewGuid(), Guid.NewGuid(), 1, "support-policy.md", PolicySeeder.Policy, null, null, null, true)], []);
            var result = await service.RunAsync(request, default);
            AnswerValidator.Validate(result.Output, request);
            Assert.Equal(item.GetProperty("expectedAction").GetString(), result.Output.ProposedAction?.Kind);
            Assert.Contains("Mod demo", result.Output.Answer);
            Assert.Equal(0, result.InputTokens + result.OutputTokens);
            Assert.All(result.Output.CitationLabels, label => Assert.Equal("S1", label));
            count++;
        }
        Assert.Equal(25, count);
    }
}
