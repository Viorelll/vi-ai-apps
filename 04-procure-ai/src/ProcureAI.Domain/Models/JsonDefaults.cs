namespace ProcureAI.Domain.Models;

using System.Text.Json;

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, Options)
        ?? throw new JsonException("Empty JSON result.");
}
