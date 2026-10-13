using System.Text.RegularExpressions;

namespace AccessReadAI.Services.Validation;

public static partial class MeaningValidator
{
    [GeneratedRegex(@"\d+(?:[.,]\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex Numbers();

    [GeneratedRegex(
        @"\b(nu|not|never|no|fără|without|ne|pas|nicht|sin)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex Negations();

    [GeneratedRegex(
        @"\b(kg|mg|g|ml|cm|mm|km|kwh|°c|unități|units|litri|litres|hours|ore|minute|minutes)\b|%",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    )]
    private static partial Regex Units();

    public static bool NumbersMatch(string original, string variant) =>
        Numbers()
            .Matches(original)
            .Select(x => x.Value.Replace(',', '.'))
            .Order()
            .SequenceEqual(
                Numbers().Matches(variant).Select(x => x.Value.Replace(',', '.')).Order()
            );

    public static bool IsSafe(string original, string variant, bool sameLanguage = true) =>
        !string.IsNullOrWhiteSpace(variant)
        && NumbersMatch(original, variant)
        && Negations().Matches(original).Count == Negations().Matches(variant).Count
        && (
            !sameLanguage
            || Units()
                .Matches(original)
                .Select(x => x.Value.ToLowerInvariant())
                .Order()
                .SequenceEqual(
                    Units().Matches(variant).Select(x => x.Value.ToLowerInvariant()).Order()
                )
        );
}
