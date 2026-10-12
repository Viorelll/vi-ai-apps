namespace ShopVisionAI.Contracts.Dto;

public sealed record Candidate(
    string Sku,
    string Name,
    string Category,
    double RetrievalScore,
    string Reason,
    string Price,
    string Currency,
    int Quantity,
    string StockLabel,
    DateTimeOffset CapturedAtUtc,
    IReadOnlyList<string> SourceIds,
    string SheetUrl
);
