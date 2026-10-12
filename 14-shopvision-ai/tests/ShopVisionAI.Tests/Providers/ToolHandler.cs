using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ShopVisionAI.Contracts.Dto;

namespace ShopVisionAI.Tests.Providers;

public sealed class ToolHandler(IReadOnlyList<Candidate> candidates, bool loop = false)
    : HttpMessageHandler
{
    public int Calls { get; private set; }
    public bool ReceivedToolResult { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken ct
    )
    {
        var input = await request.Content!.ReadAsStringAsync(ct);
        Calls++;
        ReceivedToolResult |= input.Contains("function_call_output", StringComparison.Ordinal);
        object output =
            Calls == 1 || loop
                ? new object[]
                {
                    new
                    {
                        type = "function_call",
                        name = "get_catalog_snapshot",
                        arguments = JsonSerializer.Serialize(new { sku = candidates[0].Sku }),
                        call_id = "call-1",
                        id = "fc-1",
                        status = "completed",
                    },
                }
                : new object[]
                {
                    new
                    {
                        type = "message",
                        role = "assistant",
                        content = new[]
                        {
                            new
                            {
                                type = "output_text",
                                text = JsonSerializer.Serialize(
                                    new GroundedResponse(
                                        "Candidat, fără SKU confirmat.",
                                        candidates
                                            .Select(c => new CandidateExplanation(
                                                c.Sku,
                                                "Caracteristici similare.",
                                                c.SourceIds
                                            ))
                                            .ToArray(),
                                        candidates.SelectMany(c => c.SourceIds).ToArray(),
                                        0,
                                        0,
                                        false
                                    ),
                                    new JsonSerializerOptions(JsonSerializerDefaults.Web)
                                ),
                            },
                        },
                    },
                };
        return new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(
                new
                {
                    status = "completed",
                    output,
                    usage = new { input_tokens = 20, output_tokens = 10 },
                }
            ),
        };
    }
}
