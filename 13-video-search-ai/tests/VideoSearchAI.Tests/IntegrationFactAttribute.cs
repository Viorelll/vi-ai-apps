namespace VideoSearchAI.Tests;

public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("INTEGRATION") != "1")
            Skip = "Requires real PostgreSQL and MinIO; run scripts/test.sh with INTEGRATION=1.";
    }
}
