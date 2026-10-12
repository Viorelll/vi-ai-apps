namespace ProcureAI.Application.Services;

using ProcureAI.Application.Exceptions;
using ProcureAI.Domain.DTOs;

public sealed class EvidenceValidator
{
    public void ValidateOffers(IReadOnlyList<OfferDto> offers, IReadOnlyList<SourceDto> sources, IReadOnlyList<Guid> offerDocumentIds)
    {
        if (offers.Count == 0 || offers.Count > 50 || offers.Select(x => x.Id).Distinct().Count() != offers.Count || offers.Select(x => x.DocumentId).Distinct().Count() != offers.Count)
            throw new AppException("invalid_offers", 422);
        foreach (var offer in offers)
        {
            if (offer.Id == Guid.Empty || string.IsNullOrWhiteSpace(offer.SupplierLabel) || offer.SupplierLabel.Length > 240 ||
                !offerDocumentIds.Contains(offer.DocumentId) || offer.SourceIds.Count == 0 ||
                offer.SourceIds.Any(id => !sources.Any(s => s.SourceId == id && s.DocumentId == offer.DocumentId)))
                throw new AppException("invalid_evidence", 422);
        }
        if (offerDocumentIds.Any(id => !offers.Any(o => o.DocumentId == id)))
            throw new AppException("missing_offer", 422);
    }

    public void ValidateRecommendation(RecommendationDto recommendation, IReadOnlyList<OfferScoreDto> scores,
        IReadOnlyList<SourceDto> sources, string snapshotHash)
    {
        if (scores.Count == 0 || recommendation.RecommendedOfferId != scores[0].Id || recommendation.SnapshotHash != snapshotHash)
            throw new AppException("recommendation_changed_score", 422);
        if (recommendation.Unknowns.Count > 0 || recommendation.Reasons.Count == 0 || recommendation.Reasons.Count > 10 ||
            recommendation.Reasons.Any(r => string.IsNullOrWhiteSpace(r.Text) || r.Text.Length > 3000 ||
                r.SourceIds.Count == 0 || r.SourceIds.Any(id => !sources.Any(s => s.SourceId == id))))
            throw new AppException("invalid_citations", 422);
    }
}
