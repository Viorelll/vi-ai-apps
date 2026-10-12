using System.Text.Json;
using ShopVisionAI.Contracts.Dto;

namespace ShopVisionAI.Ai.Tools;

public sealed class CatalogSnapshotTool
{
    public string Execute(string name, string arguments, IReadOnlyList<Candidate> scope)
    {
        if (name != "get_catalog_snapshot")
            throw new ArgumentException("tool_not_allowed");
        using var json = JsonDocument.Parse(arguments);
        var sku = json.RootElement.GetProperty("sku").GetString();
        var candidate =
            scope.SingleOrDefault(x => x.Sku == sku)
            ?? throw new ArgumentException("tool_source_out_of_scope");
        return JsonSerializer.Serialize(
            candidate,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
        );
    }
}
