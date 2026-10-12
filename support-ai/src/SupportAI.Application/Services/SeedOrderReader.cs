using SupportAI.Application.Abstractions;
using SupportAI.Domain.DTOs;
namespace SupportAI.Application.Services;

public sealed class SeedOrderReader : IOrderReader
{
    private static readonly OrderSnapshot[] Orders = [
    new("LAB-1001", "Delivered", "Monitor de laborator", DateTimeOffset.UtcNow.Date.AddDays(-5), true),
 new("LAB-1002", "Shipped", "Căști de laborator", DateTimeOffset.UtcNow.Date.AddDays(-2), false),
 new("LAB-1003", "Delivered", "Tastatură de laborator", DateTimeOffset.UtcNow.Date.AddDays(-40), false)];
    public IReadOnlyList<OrderSnapshot> List() => Orders;
    public OrderSnapshot? Find(string? reference) => Orders.FirstOrDefault(x => x.Reference == reference);
}
