using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using ProcureAI.Ai.Tools;
using ProcureAI.Application.Exceptions;
using ProcureAI.Domain.DTOs;
using ProcureAI.Domain.Models;

namespace ProcureAI.Ai.Providers;

public sealed class FoundryAgentProvider(HttpClient http, IConfiguration configuration, ProcurementTools tools)
{
    public async Task<DomainStepResult> ExecuteAsync(DomainStepRequest request, CancellationToken ct)
    {
        var endpoint = configuration["Foundry:OpenAiEndpoint"] ?? "";
        var key = configuration["Foundry:ApiKey"] ?? "";
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var root) || root.Scheme != "https" ||
            key.Length == 0 || key.StartsWith("replace-", StringComparison.OrdinalIgnoreCase) || root.Host.StartsWith("RESOURCE", StringComparison.OrdinalIgnoreCase))
            throw new AppException("foundry_not_configured", 503);
        root = new Uri(endpoint.TrimEnd('/') + "/");
        var deployment = configuration[$"Foundry:Deployments:{request.Step}"] ?? configuration["Foundry:ChatDeployment"] ?? "procureai-chat";
        var clock = Stopwatch.StartNew();
        long inputTokens = 0, outputTokens = 0;
        var calls = 0;
        var input = new List<object> { new { role = "user", content = JsonDefaults.Serialize(request.Input) } };
        for (var round = 0; round <= 5; round++)
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(root, "responses"));
            message.Headers.Add("api-key", key);
            message.Headers.Add("X-Correlation-ID", request.CorrelationId.ToString());
            message.Content = JsonContent.Create(new
            {
                model = deployment,
                store = false,
                max_output_tokens = Math.Max(1, request.MaxOutputTokens - outputTokens),
                instructions = AgentPrompts.For(request.Step),
                input,
                tools = new[] { tools.Definition(request.Step) },
                parallel_tool_calls = false
            });
            using var response = await http.SendAsync(message, ct);
            if (!response.IsSuccessStatusCode)
                throw new AppException((int)response.StatusCode is 401 or 403 ? "foundry_authentication_failed" : "foundry_request_failed",
                    (int)response.StatusCode == 429 ? 429 : (int)response.StatusCode >= 500 ? 503 : 422);
            using var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var json = payload.RootElement;
            if (!json.TryGetProperty("status", out var status) || status.GetString() != "completed")
                throw new AppException("foundry_response_incomplete", 422);
            if (!json.TryGetProperty("usage", out var usage) ||
                !usage.TryGetProperty("input_tokens", out var inputUsage) || !inputUsage.TryGetInt64(out var usedInput) ||
                !usage.TryGetProperty("output_tokens", out var outputUsage) || !outputUsage.TryGetInt64(out var usedOutput) ||
                usedInput < 0 || usedOutput < 0)
                throw new AppException("invalid_provider_usage", 422);
            inputTokens += usedInput;
            outputTokens += usedOutput;
            if (inputTokens + outputTokens > 15000 || outputTokens > request.MaxOutputTokens)
                throw new AppException("token_budget_exceeded", 422);
            var output = json.GetProperty("output").EnumerateArray().ToArray();
            var functions = output.Where(x => x.GetProperty("type").GetString() == "function_call").ToArray();
            if (functions.Length > 0)
            {
                if (round == 5 || calls + functions.Length > 5) throw new AppException("tool_limit_exceeded", 422);
                input.AddRange(output.Select(x => (object)x.Clone()));
                foreach (var call in functions)
                {
                    calls++;
                    var result = tools.Execute(call.GetProperty("name").GetString()!, call.GetProperty("arguments").GetString()!, request);
                    input.Add(new { type = "function_call_output", call_id = call.GetProperty("call_id").GetString(), output = result });
                }
                continue;
            }
            var text = string.Concat(output.Where(x => x.GetProperty("type").GetString() == "message")
                .SelectMany(x => x.GetProperty("content").EnumerateArray())
                .Where(x => x.GetProperty("type").GetString() == "output_text").Select(x => x.GetProperty("text").GetString()));
            if (string.IsNullOrWhiteSpace(text) || text.Length > 60000) throw new AppException("invalid_agent_json", 422);
            try { using var parsed = JsonDocument.Parse(text); }
            catch (JsonException) { throw new AppException("invalid_agent_json", 422); }
            return new DomainStepResult("completed", text, deployment, inputTokens, outputTokens, clock.ElapsedMilliseconds);
        }
        throw new AppException("tool_limit_exceeded", 422);
    }
}
