namespace TutorAI.Domain.Rules;

public static class AnswerGrader
{
    public static decimal Grade(string answer, string expected) =>
        string.Equals(answer.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase) ? 1m : 0m;
}
