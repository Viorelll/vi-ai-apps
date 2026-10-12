namespace ProcureAI.Domain.DTOs;

public sealed record OfferDto(Guid Id, Guid DocumentId, string SupplierLabel, string? Currency,
    decimal? Quantity, string? Unit, decimal? Net, decimal? Shipping, int? LeadDays,
    bool? MeetsRequirements, IReadOnlyList<Guid> SourceIds);
