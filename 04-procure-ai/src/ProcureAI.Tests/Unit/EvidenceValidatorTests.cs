using ProcureAI.Application.Exceptions;
using ProcureAI.Application.Services;
using ProcureAI.Domain.DTOs;

namespace ProcureAI.Tests.Unit;

public sealed class EvidenceValidatorTests
{
    private readonly SourceDto source = new(Guid.NewGuid(), Guid.NewGuid(), "offer.json", 1, "EUR 1000 shipping 50", null);
    [Fact]
    public void ForeignDocumentEvidenceIsRejected()
    {
        var offer = new OfferDto(Guid.NewGuid(), Guid.NewGuid(), "A", "EUR", 10, "unități", 1000, 50, 7, true, [source.SourceId]);
        Assert.Equal("invalid_evidence", Assert.Throws<AppException>(() => new EvidenceValidator().ValidateOffers([offer], [source], [source.DocumentId])).Code);
    }
    [Theory]
    [InlineData("winner")]
    [InlineData("snapshot")]
    [InlineData("citation")]
    public void ModelCannotChangeWinnerSnapshotOrScope(string mutation)
    {
        var id = Guid.NewGuid();
        var report = new RecommendationDto(mutation == "winner" ? Guid.NewGuid() : id,
            mutation == "snapshot" ? "forged" : "hash", [new ReasonDto("Reason", [mutation == "citation" ? Guid.NewGuid() : source.SourceId])], []);
        Assert.Throws<AppException>(() => new EvidenceValidator().ValidateRecommendation(report, [new OfferScoreDto(id, 1050, .79m)], [source], "hash"));
    }
}
