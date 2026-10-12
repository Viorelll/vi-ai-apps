namespace ContractAI.Tests.Integration;

public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CONTRACTAI_TEST_DB")))
            Skip =
                "Set CONTRACTAI_TEST_DB and CONTRACTAI_TEST_S3 to run against real PostgreSQL and S3.";
    }
}
