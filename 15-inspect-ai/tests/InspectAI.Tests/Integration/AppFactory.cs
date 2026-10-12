using InspectAI.Infrastructure.Abstractions;
using InspectAI.Infrastructure.Extraction;
using InspectAI.Tests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InspectAI.Tests.Integration;

public sealed class AppFactory(bool runWorker = true, string? connectionString = null)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(
            (_, c) =>
                c.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:App"] =
                            connectionString
                            ?? Environment.GetEnvironmentVariable("INSPECTAI_TEST_DB"),
                        ["Storage:Endpoint"] = Environment.GetEnvironmentVariable(
                            "INSPECTAI_TEST_S3"
                        ),
                        ["Storage:AccessKey"] = "inspectai-test",
                        ["Storage:SecretKey"] = "inspectai-test-password",
                        ["Storage:Bucket"] = "inspectai-tests",
                        ["Internal:Key"] = "test-only-internal-key",
                    }
                )
        );
        builder.ConfigureServices(s =>
        {
            if (!runWorker)
            {
                var worker = s.FirstOrDefault(d =>
                    d.ImplementationType == typeof(InspectAI.Services.Jobs.DurableWorker)
                );
                if (worker is not null)
                    s.Remove(worker);
            }
            s.RemoveAll<IAiClient>();
            s.AddSingleton<DeterministicAiClient>(p =>
                new(
                    new DocumentExtractor(
                        new HttpClient(),
                        p.GetRequiredService<IObjectStore>(),
                        p.GetRequiredService<IConfiguration>()
                    )
                )
            );
            s.AddSingleton<IAiClient>(p => p.GetRequiredService<DeterministicAiClient>());
        });
    }
}
