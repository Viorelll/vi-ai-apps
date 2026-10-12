using System.Text.RegularExpressions;
using ContractAI.Domain.DTOs;

namespace ContractAI.Domain.Services;

public static partial class ClauseComparer
{
    public static ClausePair[] Compare(ClauseSource[] left, ClauseSource[] right)
    {
        var remaining = right.ToList();
        var result = new List<ClausePair>();
        foreach (var l in left)
        {
            var exact = remaining.FirstOrDefault(r => Normalize(l.Text) == Normalize(r.Text));
            var best =
                exact
                ?? remaining.OrderByDescending(r => Similarity(l.Text, r.Text)).FirstOrDefault();
            if (best is not null && (exact is not null || Similarity(l.Text, best.Text) >= .35))
            {
                remaining.Remove(best);
                result.Add(
                    new(
                        l.Id,
                        best.Id,
                        exact is not null ? "unchanged" : "modified",
                        l.Text,
                        best.Text
                    )
                );
            }
            else
                result.Add(new(l.Id, null, "removed", l.Text, null));
        }
        result.AddRange(remaining.Select(r => new ClausePair(null, r.Id, "added", null, r.Text)));
        return result.ToArray();
    }

    public static string Normalize(string text) =>
        Numbering().Replace(text.ToLowerInvariant().Trim(), "");

    private static double Similarity(string a, string b)
    {
        var x = Words().Matches(Normalize(a)).Select(m => m.Value).ToHashSet();
        var y = Words().Matches(Normalize(b)).Select(m => m.Value).ToHashSet();
        return x.Count + y.Count == 0 ? 0 : 2.0 * x.Intersect(y).Count() / (x.Count + y.Count);
    }

    [GeneratedRegex(@"^(?:art\.?\s*)?\d+[.)]?\s*", RegexOptions.IgnoreCase)]
    private static partial Regex Numbering();

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex Words();
}
