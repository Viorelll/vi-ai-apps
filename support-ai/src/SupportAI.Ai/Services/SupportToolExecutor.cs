using System.Text.Json;
using SupportAI.Domain.DTOs;
using SupportAI.Domain.Models;
namespace SupportAI.Ai.Services;

public sealed class SupportToolExecutor
{
    public object Execute(string name, JsonElement arguments, DomainStepRequest request)
    {
        switch (name)
        {
            case "get_order_status":
                if (arguments.GetProperty("reference").GetString() != request.Order?.Reference)
                    throw new SupportException("tool_scope_denied", 422);
                return request.Order ?? throw new SupportException("order_not_found", 404);
            case "search_policy":
                var query = arguments.GetProperty("query").GetString();
                if (string.IsNullOrWhiteSpace(query) || query.Length > 1000)
                    throw new SupportException("invalid_tool_arguments", 422);
                return request.Sources.Where(s => s.IsPolicy).ToArray();
            case "inspect_attachment":
                var label = arguments.GetProperty("label").GetString();
                return request.Sources.SingleOrDefault(s => s.Label == label && !s.IsPolicy) ?? throw new SupportException("tool_scope_denied", 422);
            case "propose_ticket":
            case "propose_return":
                var summary = arguments.GetProperty("summary").GetString();
                if (string.IsNullOrWhiteSpace(summary) || summary.Length > 500)
                    throw new SupportException("invalid_tool_arguments", 422);
                if (name == "propose_return" && request.Order?.ReturnEligible != true)
                    throw new SupportException("return_ineligible", 409);
                return new
                {
                    kind = name == "propose_return" ? "return" : "ticket",
                    summary,
                    state = "Draft",
                    requiresConfirmation = true,
                    executed = false
                };
            default:
                throw new SupportException("unknown_tool", 422);
        }
    }
    public object[] Schemas() => [Schema("get_order_status", "Read the selected laboratory order.", "reference"), Schema("search_policy", "Read active policies in the case scope.", "query"), Schema("inspect_attachment", "Read an attachment source by its allowed label.", "label"), Schema("propose_ticket", "Prepare a local ticket. Never execute it.", "summary"), Schema("propose_return", "Prepare a return only for an eligible delivered order. Never execute it.", "summary")];
    private static object Schema(string name, string description, string arg) => new { type = "function", name, description, strict = true, parameters = new { type = "object", properties = new Dictionary<string, object> { { arg, new { type = "string" } } }, required = new[] { arg }, additionalProperties = false } };
}
