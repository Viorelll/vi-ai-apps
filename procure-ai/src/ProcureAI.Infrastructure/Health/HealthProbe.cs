namespace ProcureAI.Infrastructure.Health;

public static class HealthProbe
{
    public static async Task<int> CheckAsync(string url)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var response = await http.GetAsync(url);
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (HttpRequestException) { return 1; }
        catch (TaskCanceledException) { return 1; }
    }
}
