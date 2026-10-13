using Microsoft.EntityFrameworkCore;
using TutorAI.Domain.Entities;

namespace TutorAI.Database.Persistence;

public static class SeedData
{
    public static async Task InitializeAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.LearningSources.AnyAsync(ct))
            return;
        var checkedAt = new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero);
        db.AddRange(
            new LearningSource
            {
                Id = Guid.Parse("11111111-1111-4111-8111-111111111111"),
                Title = "Retrieval augmented generation",
                Objective = "RAG",
                OfficialUrl =
                    "https://learn.microsoft.com/en-us/azure/search/retrieval-augmented-generation-overview",
                CheckedAtUtc = checkedAt,
                Text =
                    "Retrieval-augmented generation (RAG) is a pattern that extends LLM capabilities by grounding responses in your proprietary content. While conceptually simple, RAG implementations face significant challenges. LLMs accept limited token inputs. Your retrieval system must return highly relevant, concise results - not exhaustive document dumps.",
            },
            new LearningSource
            {
                Id = Guid.Parse("22222222-2222-4222-8222-222222222222"),
                Title = "DbContext lifetime",
                Objective = ".NET / EF Core",
                OfficialUrl = "https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/",
                CheckedAtUtc = checkedAt,
                Text =
                    "DbContext is Not thread-safe. Don't share contexts between threads. Make sure to await all async calls before continuing to use the context instance.",
            },
            new LearningSource
            {
                Id = Guid.Parse("33333333-3333-4333-8333-333333333333"),
                Title = "Foundry tools and function calling",
                Objective = "Foundry agents",
                OfficialUrl =
                    "https://learn.microsoft.com/en-us/azure/ai-foundry/openai/how-to/function-calling",
                CheckedAtUtc = checkedAt,
                Text =
                    "The model generates function calls and structured arguments based on the functions you specify. Your application executes the functions and returns the results to the model, so you remain in control of the actions taken.",
            }
        );
        await db.SaveChangesAsync(ct);
    }
}
