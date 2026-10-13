using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace EvalAI.Domain.Models;

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string value) => JsonSerializer.Deserialize<T>(value, Options) ?? throw new AppException(502, "invalid_json", "Invalid JSON data.");
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
