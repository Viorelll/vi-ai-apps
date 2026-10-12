using System.Text.Json;
using InvoiceAI.Contracts.Dto;
using InvoiceAI.Infrastructure.Ai;
namespace InvoiceAI.Ai.Tools;

public sealed class InvoiceToolExecutor
{
    public static readonly string[] Names = ["read_extracted_fields", "calculate_totals", "find_possible_duplicate", "explain_validation_issue", "prepare_export"];
    public string Execute(string name, string arguments, ExplainRequest request)
    {
        using var args = JsonDocument.Parse(arguments);
        if (args.RootElement.ValueKind != JsonValueKind.Object || args.RootElement.EnumerateObject().Any())
            throw new GatewayException("TOOL_ARGUMENT_INVALID", false);
        decimal? Amount(string path)
        {
            var field = request.Fields.FirstOrDefault(x => x.Path == path);
            var value = field?.CorrectedValue ?? field?.Value;
            return value is JsonElement v && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var n) ? n : null;
        }
        object result = name switch
        {
            "read_extracted_fields" => request.Fields,
            "calculate_totals" => new { subtotal = Amount("subtotal"), tax = Amount("tax"), total = Amount("total"), difference = Amount("total") - Amount("subtotal") - Amount("tax"), lineSum = request.Lines.All(x => x.Amount is not null) ? request.Lines.Sum(x => x.Amount) : null },
            "find_possible_duplicate" => new { candidate = request.Issues.Any(x => x.Code == "POSSIBLE_DUPLICATE"), automaticDeletion = false },
            "explain_validation_issue" => request.Issues,
            "prepare_export" => new { draft = true, exportAllowed = !request.Issues.Any(x => x.Severity == "error"), externalWrites = false },
            _ => throw new GatewayException("TOOL_NOT_ALLOWED", false)
        };
        return JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
    public static object[] Definitions() => Names.Select(name => (object)new { type = "function", name, description = $"Read-only {name} for the server-scoped current invoice. No external writes.", parameters = new { type = "object", properties = new { }, required = Array.Empty<string>(), additionalProperties = false }, strict = true }).ToArray();
}
