namespace TutorAI.Domain.Rules;

public static class DifficultyRule
{
    public static int Next(int current, IReadOnlyList<decimal> scores)
    {
        if (current is < 1 or > 3 || scores.Any(x => x is < 0 or > 1))
            throw new ArgumentException("invalid_level_or_score");
        if (scores.Count < 2)
            return current;
        var last = scores.TakeLast(2).ToArray();
        if (last.All(x => x >= 0.8m))
            return Math.Min(3, current + 1);
        if (last.All(x => x <= 0.4m))
            return Math.Max(1, current - 1);
        return current;
    }
}
