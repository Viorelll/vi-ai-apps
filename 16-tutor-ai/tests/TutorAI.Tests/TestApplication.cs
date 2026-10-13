using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TutorAI.Tests;

public sealed class TestApplication : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(
            "ConnectionStrings:App",
            "Host=localhost;Port=54336;Database=tutorai_tests;Username=tutorai;Password=tutorai-test-password"
        );
        builder.UseSetting("Worker:Disabled", "true");
        builder.UseSetting("Ai:DemoMode", "true");
        builder.UseSetting("Storage:Endpoint", "http://localhost:59016");
    }
}
