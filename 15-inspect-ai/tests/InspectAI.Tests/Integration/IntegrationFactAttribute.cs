namespace InspectAI.Tests.Integration;

public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("INSPECTAI_TEST_DB")))
            Skip =
                "Set INSPECTAI_TEST_DB and INSPECTAI_TEST_S3 to run against real PostgreSQL and S3.";
    }
}
