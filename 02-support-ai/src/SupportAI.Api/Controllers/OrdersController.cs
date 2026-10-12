using Microsoft.AspNetCore.Mvc;
using SupportAI.Application.Abstractions;
using SupportAI.Domain.DTOs;
namespace SupportAI.Api.Controllers;

[ApiController]
[Route("api/support/orders")]
public sealed class OrdersController(IOrderReader orders) : ControllerBase
{
    [HttpGet] public IReadOnlyList<OrderSnapshot> List() => orders.List();
    [HttpGet("{reference}")] public ActionResult<OrderSnapshot> Get(string reference) => orders.Find(reference) is { } order ? Ok(order) : NotFound();
}
