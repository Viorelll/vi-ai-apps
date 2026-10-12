namespace SupportAI.Domain.DTOs;

public sealed record OrderSnapshot(string Reference, string Status, string Product, DateTimeOffset DeliveredAtUtc, bool ReturnEligible);
