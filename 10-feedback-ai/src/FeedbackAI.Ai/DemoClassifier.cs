public static class DemoClassifier
{
    public static ClassificationResult Classify(string text)
    {
        var normalized = text.ToLowerInvariant();
        var topic = normalized.Contains("livr") || normalized.Contains("delivery") || normalized.Contains("curier") || normalized.Contains("întârzi") || normalized.Contains("intarzi")
            ? "delivery"
            : normalized.Contains("produs") || normalized.Contains("product") || normalized.Contains("defect")
                ? "product"
                : normalized.Contains("informa") || normalized.Contains("detalii") || normalized.Contains("information") || normalized.Contains("garanție") || normalized.Contains("garantie")
                    ? "information"
                    : "other";
        var negative = new[] { "întârzi", "intarzi", "defect", "rău", "rau", "nemulțumit", "nemultumit", "problem", "broken", "late" }.Any(normalized.Contains);
        var high = new[] { "urgent", "imediat", "periculos", "safety", "danger" }.Any(normalized.Contains);
        return new ClassificationResult(topic, negative ? "negative" : "neutral", high ? "high" : "normal", "Clasificare demonstrativă deterministă.", "Demo", "rules-v1");
    }
}
