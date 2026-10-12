namespace InvoiceAI.Infrastructure.Health;

public static class HttpHealthProbe
{
    public static async Task<int> CheckAsync(bool internalAuthentication)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
            if (internalAuthentication)
                client.DefaultRequestHeaders.Add("X-Internal-Key", Environment.GetEnvironmentVariable("Internal__Key") ?? "dummy-internal-key-change-me");
            using var response = await client.GetAsync("http://127.0.0.1:8080/health/ready");
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch { return 1; }
    }
}
