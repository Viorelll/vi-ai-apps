namespace ShopVisionAI.Contracts.Dto;

public sealed record Observation(
    string SourceId,
    string Kind,
    string Attribute,
    string Value,
    Region? Region
);
