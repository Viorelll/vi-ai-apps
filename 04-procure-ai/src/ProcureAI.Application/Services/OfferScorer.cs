namespace ProcureAI.Application.Services;

using ProcureAI.Application.Exceptions;
using ProcureAI.Domain.DTOs;

public sealed class OfferScorer
{
    public IReadOnlyList<OfferScoreDto> Score(IReadOnlyList<OfferDto> offers, CriteriaDto criteria)
    {
        if (criteria.MaxDays <= 0 || criteria.Quantity <= 0 || offers.Count == 0)
            throw new AppException("offers_required", 422);
        if (offers.Any(x => x.Net is null || x.Shipping is null || x.LeadDays is null ||
            x.Quantity is null || x.MeetsRequirements is null || string.IsNullOrWhiteSpace(x.Currency) || string.IsNullOrWhiteSpace(x.Unit)))
            throw new AppException("incomplete_offers", 422);
        if (offers.Any(x => x.Currency != criteria.Currency || x.Quantity != criteria.Quantity || x.Unit != criteria.Unit ||
            x.Net < 0 || x.Shipping < 0 || x.LeadDays < 0))
            throw new AppException("incomparable_offers", 422);
        var eligible = offers.Where(x => x.MeetsRequirements == true && x.LeadDays <= criteria.MaxDays).ToArray();
        if (eligible.Length == 0) throw new AppException("no_eligible_offers", 422);
        var minCost = eligible.Min(x => x.Net!.Value + x.Shipping!.Value);
        if (minCost <= 0) throw new AppException("cost_required", 422);
        return eligible.Select(x => new OfferScoreDto(x.Id, x.Net!.Value + x.Shipping!.Value,
                0.7m * minCost / (x.Net.Value + x.Shipping.Value) + 0.3m * (1m - (decimal)x.LeadDays!.Value / criteria.MaxDays)))
            .OrderByDescending(x => x.Score).ThenBy(x => x.Cost).ThenBy(x => x.Id).ToArray();
    }
}
