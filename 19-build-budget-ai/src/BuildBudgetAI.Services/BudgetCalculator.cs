using System.Globalization;
using BuildBudgetAI.Domain.Dto;
using BuildBudgetAI.Domain.Entities;
using BuildBudgetAI.Domain.Models;

namespace BuildBudgetAI.Services;

public static class BudgetCalculator
{
    public static readonly string[] Units = ["m", "m²", "m³", "kg", "buc", "pachet"];

    public static string Money(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);

    public static string Number(decimal value) =>
        value.ToString("0.######", CultureInfo.InvariantCulture);

    public static void ValidateNeed(NeedInput n)
    {
        if (
            string.IsNullOrWhiteSpace(n.Label)
            || n.Label.Length > 200
            || !Units.Contains(n.Unit)
            || n.Quantity < 0
            || n.Quantity > 100000000
            || n.WastePercent is < 0 or > 100
        )
            throw new DomainException("invalid_need");
    }

    public static decimal CalculateKnownTotal(
        decimal quantity,
        decimal wastePercent,
        decimal unitPrice,
        decimal transport
    )
    {
        if (quantity < 0 || wastePercent is < 0 or > 100 || unitPrice < 0 || transport < 0)
            throw new DomainException("invalid_budget_input");
        return decimal.Round(
            checked(quantity * (1 + wastePercent / 100) * unitPrice + transport),
            2,
            MidpointRounding.AwayFromZero
        );
    }

    public static BudgetLineDto Calculate(
        MaterialNeed need,
        MaterialOffer offer,
        OfferLine line,
        string currency,
        string taxBasis
    )
    {
        var unknowns = new List<string>();
        if (!line.Confirmed)
            unknowns.Add("Preț neconfirmat");
        if (offer.Currency != currency)
            unknowns.Add("Monedă diferită");
        if (offer.TaxBasis == "Unknown" || offer.TaxBasis != taxBasis)
            unknowns.Add("Bază fiscală necunoscută sau diferită");
        if (line.Transport is null)
            unknowns.Add("Transport necunoscut");
        decimal? price = line.Price;
        decimal adjusted = need.Quantity * (1 + need.WastePercent / 100);
        if (line.Unit != need.Unit)
        {
            if (line.Unit == "pachet" && need.Unit == "buc" && line.PackQuantity > 0)
            {
                price = line.Price / line.PackQuantity;
                adjusted =
                    decimal.Ceiling(adjusted / line.PackQuantity.Value) * line.PackQuantity.Value;
            }
            else
            {
                unknowns.Add("Unități necomparabile");
                price = null;
            }
        }
        decimal? total =
            unknowns.Count == 0 && price.HasValue
                ? CalculateKnownTotal(adjusted, 0, price.Value, line.Transport!.Value)
                : null;
        string formula = total.HasValue
            ? $"{Number(adjusted)} {need.Unit} × {Number(price!.Value)} + {Number(line.Transport!.Value)} = {Money(total.Value)} {currency}"
            : "Calcul incomplet — confirmă câmpurile necunoscute";
        if (offer.TaxBasis == "Excluded" && total.HasValue)
            unknowns.Add("Taxele suplimentare nu sunt incluse");
        return new(
            need.Id,
            line.Id,
            need.Label,
            need.Unit,
            Number(adjusted),
            price.HasValue ? Number(price.Value) : null,
            total.HasValue ? Money(total.Value) : null,
            formula,
            unknowns
        );
    }
}
