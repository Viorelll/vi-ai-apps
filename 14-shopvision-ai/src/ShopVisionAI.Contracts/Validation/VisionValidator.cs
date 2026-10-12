using ShopVisionAI.Contracts.Dto;

namespace ShopVisionAI.Contracts.Validation;

public static class VisionValidator
{
    public static void Validate(VisionResponse response, string source)
    {
        if (
            response.Status is not ("Completed" or "NeedsInput")
            || response.InputTokens < 0
            || response.OutputTokens < 0
            || response.InputTokens + response.OutputTokens > 15000
            || response.Observations.Count > 30
        )
            throw new ArgumentException("invalid_ai_response");
        foreach (var o in response.Observations)
        {
            if (
                o.SourceId != source
                || o.Kind is not ("visible" or "inferred" or "unknown")
                || string.IsNullOrWhiteSpace(o.Attribute)
                || o.Attribute.Length > 80
                || string.IsNullOrWhiteSpace(o.Value)
                || o.Value.Length > 2000
            )
                throw new ArgumentException("invalid_observation");
            if (
                o.Region is { } b
                && (
                    !double.IsFinite(b.X)
                    || !double.IsFinite(b.Y)
                    || !double.IsFinite(b.Width)
                    || !double.IsFinite(b.Height)
                    || b.X < 0
                    || b.Y < 0
                    || b.Width <= 0
                    || b.Height <= 0
                    || b.X + b.Width > 1
                    || b.Y + b.Height > 1
                )
            )
                throw new ArgumentException("invalid_region");
            if (
                o.Kind == "visible"
                && o.Attribute is "material" or "power" or "authenticity" or "certification"
            )
                throw new ArgumentException("unobservable_attribute");
        }
    }
}
