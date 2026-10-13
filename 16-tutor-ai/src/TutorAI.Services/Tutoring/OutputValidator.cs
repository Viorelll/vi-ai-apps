using TutorAI.Domain.DTOs;
using TutorAI.Domain.Models;

namespace TutorAI.Services.Tutoring;

public static class OutputValidator
{
    public static void Validate(TutorOutput output, AiRequest request)
    {
        [System.Diagnostics.CodeAnalysis.DoesNotReturn]
        static void Fail() =>
            throw new AppException(
                502,
                "invalid_tutor_output",
                "AI output violates the tutoring contract."
            );
        if (
            string.IsNullOrWhiteSpace(output.Explanation)
            || output.Explanation.Length > 12000
            || output.Citations is null
            || output.Criteria is null
            || output.Citations.Length is < 1 or > 12
        )
            Fail();
        foreach (var citation in output.Citations!)
        {
            var source = request.Sources.SingleOrDefault(x => x.Id == citation.SourceId);
            if (
                source is null
                || string.IsNullOrWhiteSpace(citation.Quote)
                || citation.Quote.Length > 3000
                || !source.Text.Contains(citation.Quote, StringComparison.Ordinal)
            )
                Fail();
        }
        if (
            output.Criteria.Length > 8
            || output.Criteria.Any(x =>
                x is null
                || string.IsNullOrWhiteSpace(x.Criterion)
                || x.Criterion.Length > 300
                || x.Feedback?.Length > 4000
            )
        )
            Fail();
        if (request.Step == "exercise")
        {
            var e = output.Exercise;
            if (
                e is null
                || e.Kind is not ("single_choice" or "open_answer")
                || string.IsNullOrWhiteSpace(e.Prompt)
                || e.Prompt.Length > 4000
                || e.Rubric is null
                || e.Options is null
            )
            {
                Fail();
                return;
            }
            if (
                e.Kind != request.Question
                || string.IsNullOrWhiteSpace(e.Rubric.ExpectedAnswer)
                || e.Rubric.Criteria is null
                || e.Rubric.Criteria.Length is < 1 or > 8
                || e.Rubric.Version != "rubric-v1"
                || e.Rubric.ExpectedAnswer.Length > 8000
                || e.Rubric.Criteria.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 300)
                || e.Rubric.Criteria.Distinct().Count() != e.Rubric.Criteria.Length
                || e.Options.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 1000)
            )
                Fail();
            if (
                e.Kind == "single_choice"
                && (
                    e.Options.Length is < 2 or > 6
                    || e.Options.Distinct(StringComparer.OrdinalIgnoreCase).Count()
                        != e.Options.Length
                    || !e.Options.Contains(e.Rubric.ExpectedAnswer)
                )
            )
                Fail();
            if (e.Kind == "open_answer" && e.Options.Length != 0)
                Fail();
        }
        else if (output.Exercise is not null)
            Fail();
        if (
            request.Step == "grade"
            && (
                output.Score is null or < 0 or > 1
                || output.Criteria.Length == 0
                || output.Criteria.Any(x =>
                    x.Score is < 0 or > 1 || string.IsNullOrWhiteSpace(x.Feedback)
                )
                || request.Rubric is null
                || !output
                    .Criteria.Select(x => x.Criterion)
                    .Order()
                    .SequenceEqual(request.Rubric.Criteria.Order())
            )
        )
            Fail();
    }
}
