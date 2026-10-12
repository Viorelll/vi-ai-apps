using System.Text.Json;
using ProcureAI.Application.Exceptions;
using ProcureAI.Domain.DTOs;
using ProcureAI.Domain.Models;

namespace ProcureAI.Ai.Tools;

public sealed class ProcurementTools
{
    public static string NameFor(string role) => role switch
    {
        "needs" => "validate_requirements",
        "suppliers" => "extract_offer",
        "compare" => "score_offers",
        "recommend" => "build_recommendation",
        "chat" => "search_supplier_evidence",
        _ => throw new AppException("unsupported_agent_role")
    };

    public object Definition(string role) => new
    {
        type = "function",
        name = NameFor(role),
        description = "Read the server-validated procurement snapshot for this role; no external actions.",
        strict = true,
        parameters = new { type = "object", properties = new Dictionary<string, object>(), required = Array.Empty<string>(), additionalProperties = false }
    };

    public string Execute(string name, string arguments, DomainStepRequest request)
    {
        if (name != NameFor(request.Step)) throw new AppException("tool_not_allowed", 422);
        using var args = JsonDocument.Parse(arguments);
        if (args.RootElement.ValueKind != JsonValueKind.Object || args.RootElement.EnumerateObject().Any())
            throw new AppException("invalid_tool_arguments", 422);
        object value = request.Step switch
        {
            "needs" => request.Input.Criteria,
            "suppliers" or "chat" => request.Input.Sources,
            "compare" => request.Input.Scores,
            _ => new { request.Input.Offers, request.Input.Scores, request.Input.Sources, request.Input.SnapshotHash }
        };
        return JsonDefaults.Serialize(value);
    }
}
