using SupportAI.Domain.DTOs;
namespace SupportAI.Application.Abstractions;

public interface IOrderReader
{
    IReadOnlyList<OrderSnapshot> List(); OrderSnapshot? Find(string? reference);
}
