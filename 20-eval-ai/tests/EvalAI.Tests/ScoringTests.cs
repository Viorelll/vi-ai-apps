using System.Text.Json;
using EvalAI.Domain.DTOs;
using EvalAI.Domain.Entities;
using EvalAI.Domain.Models;
using EvalAI.Domain.Services;
using Xunit;
namespace EvalAI.Tests;

public sealed class ScoringTests
{
    [Theory]
    [InlineData("{\"answer\":5}", "{\"answer\":5.0}", true)]
    [InlineData("{\"answer\":5}", "{\"answer\":6}", false)]
    [InlineData("{\"answer\":5}", "{\"answer\":\"5\"}", false)]
    [InlineData("{\"items\":[1,2]}", "{\"items\":[1,2]}", true)]
    [InlineData("{\"items\":[1,2]}", "{\"items\":[2,1]}", false)]
    public void ChecksCompareExpectedSubset(string expected, string output, bool passed) { Assert.Equal(passed, ScoringRules.Check(output, expected, []).Passed); }
    [Fact] public void InvalidOutputFails() { Assert.False(ScoringRules.Check("not json", "{}", []).Passed); }
    [Fact] public void CitationsRequireScopedIdAndVerbatimText() { var source = new EvidenceSource(Guid.NewGuid(), Guid.NewGuid(), 1, "Verified quotation", "seed.txt", null); string output(Guid id, string quote) => JsonDefaults.Serialize(new { answer = 5, citations = new[] { new { chunkId = id, quote } } }); Assert.True(ScoringRules.Check(output(source.ChunkId, "Verified"), "{\"answer\":5}", [source]).Passed); Assert.False(ScoringRules.Check(output(Guid.NewGuid(), "Verified"), "{}", [source]).Passed); Assert.False(ScoringRules.Check(output(source.ChunkId, "invented"), "{}", [source]).Passed); Assert.False(ScoringRules.Check("{\"answer\":5}", "{}", [source]).Passed); }
    [Fact] public void MissingRatesRemainUnknown() { Assert.Null(ScoringRules.KnownTextCost(new(100, 20, "a"), null)); Assert.Null(ScoringRules.KnownTextCost(new(100, 20, "a"), new() { InputPerMillion = 1 })); }
    [Fact] public void CostUsesDecimalMillionUnit() { Assert.Equal(0.00021m, ScoringRules.KnownTextCost(new(100, 20, "a"), new() { InputPerMillion = 1.5m, OutputPerMillion = 3m })); }
    [Fact] public void NegativeUsageAndRatesRejected() { Assert.Throws<ArgumentException>(() => ScoringRules.KnownTextCost(new(-1, 1, "a"), null)); Assert.Throws<ArgumentException>(() => ScoringRules.KnownTextCost(new(1, 1, "a"), new() { InputPerMillion = -1, OutputPerMillion = 1 })); }
    [Fact] public void BaselineRequiresAllScopeVersions() { var scope = new EvalScope("v1", "hash", "evidence", "checks-v1", "judge-v1", "safety-v1"); ScoringRules.RequireSameBaseline(scope, scope); Assert.Throws<AppException>(() => ScoringRules.RequireSameBaseline(scope, scope with { DatasetVersion = "v2" })); Assert.Throws<AppException>(() => ScoringRules.RequireSameBaseline(scope, scope with { EvidenceHash = "changed" })); Assert.Throws<AppException>(() => ScoringRules.RequireSameBaseline(scope, scope with { RubricVersion = "changed" })); }
    [Fact] public void PercentilesUseNearestRank() { Assert.Equal(2, ScoringRules.Percentile([1, 2, 3, 4], 0.5m)); Assert.Equal(4, ScoringRules.Percentile([1, 2, 3, 4], 0.95m)); }
}
