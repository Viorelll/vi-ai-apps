using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TutorAI.Database.Persistence;
using Xunit;

namespace TutorAI.Tests;

public sealed class TutorFixture : IAsyncLifetime
{
    public TestApplication App { get; } = new();
    public HttpClient Http { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Http = App.CreateClient();
        await using var db = await App
            .Services.GetRequiredService<IDbContextFactory<AppDbContext>>()
            .CreateDbContextAsync();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await SeedData.InitializeAsync(db);
    }

    public Task DisposeAsync()
    {
        Http.Dispose();
        App.Dispose();
        return Task.CompletedTask;
    }
}

[CollectionDefinition("Postgres")]
public sealed class PostgresCollection : ICollectionFixture<TutorFixture> { }
