using Microsoft.AspNetCore.Mvc;

namespace ImageAI.Api.Middleware;

public static class ValidationProblemFactory
{
    public static IActionResult Create(ActionContext context) =>
        new BadRequestObjectResult(
            new ProblemDetails
            {
                Status = 400,
                Title = "invalid_request",
                Extensions =
                {
                    ["code"] = "invalid_request",
                    ["traceId"] = context.HttpContext.TraceIdentifier,
                },
            }
        );
}
