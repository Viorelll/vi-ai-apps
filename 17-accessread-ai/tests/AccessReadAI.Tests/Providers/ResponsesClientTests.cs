using System.Net;
using System.Text.Json;
using AccessReadAI.Ai.Models;
using AccessReadAI.Ai.Providers;
using AccessReadAI.Contracts;
using Microsoft.Extensions.Options;

namespace AccessReadAI.Tests.Providers;

public sealed class ResponsesClientTests
{
    private static ResponsesClient Client(string json) =>
        new(
            new HttpClient(
                new FakeHttpHandler(_ =>
                    Task.FromResult(
                        new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(json),
                        }
                    )
                )
            ),
            Options.Create(new ProviderOptions())
        );

    [Fact]
    public async Task CollectsMessageContentAndUsage()
    {
        var result = await Client(
                """{"status":"completed","usage":{"input_tokens":12,"output_tokens":5},"output":[{"type":"message","content":[{"type":"output_text","text":"hello"}]}]}"""
            )
            .GenerateAsync("system", "input", new Dictionary<string, string>(), default);
        Assert.Equal("hello", result.Text);
        Assert.Equal(12, result.Input);
        Assert.Equal(5, result.Output);
    }

    [Fact]
    public async Task RejectsIncompleteResponse()
    {
        await Assert.ThrowsAsync<ProviderFault>(() =>
            Client("""{"status":"incomplete","output":[]}""")
                .GenerateAsync("system", "input", new Dictionary<string, string>(), default)
        );
    }

    [Fact]
    public async Task RejectsOutOfScopeTools()
    {
        await Assert.ThrowsAsync<ProviderFault>(() =>
            Client(
                    """{"status":"completed","output":[{"type":"function_call","name":"read_document_block","arguments":"{\"sourceId\":\"other\"}","call_id":"c1"}]}"""
                )
                .GenerateAsync(
                    "system",
                    "input",
                    new Dictionary<string, string> { { "allowed", "text" } },
                    default
                )
        );
    }

    [Fact]
    public async Task ExecutesAllowedFunctionWithMatchingCallId()
    {
        var count = 0;
        var handler = new FakeHttpHandler(async request =>
        {
            var body = await request.Content!.ReadAsStringAsync();
            count++;
            if (count == 1)
                return new(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"status":"completed","output":[{"type":"function_call","name":"read_document_block","arguments":"{\"sourceId\":\"allowed\"}","call_id":"call-1"}]}"""
                    ),
                };
            Assert.Contains("function_call_output", body);
            Assert.Contains("call-1", body);
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"status":"completed","output":[{"type":"message","content":[{"type":"output_text","text":"answer"}]}]}"""
                ),
            };
        });
        var client = new ResponsesClient(
            new HttpClient(handler),
            Options.Create(new ProviderOptions())
        );
        var result = await client.GenerateAsync(
            "system",
            "input",
            new Dictionary<string, string> { { "allowed", "text" } },
            default
        );
        Assert.Equal("answer", result.Text);
        Assert.Equal(2, count);
    }

    [Theory]
    [InlineData(429, true)]
    [InlineData(503, true)]
    [InlineData(401, false)]
    [InlineData(400, false)]
    public async Task ClassifiesProviderFailures(int status, bool transient)
    {
        using var response = new HttpResponseMessage((HttpStatusCode)status);
        var error = await Assert.ThrowsAsync<ProviderFault>(() =>
            ProviderHttp.EnsureAsync(response, default)
        );
        Assert.Equal(transient, error.Transient);
    }

    [Theory]
    [InlineData("http://resource.test/documentintelligence/x")]
    [InlineData("https://evil.test/documentintelligence/x")]
    [InlineData("https://resource.test/other")]
    public void RejectsUnsafeOperationUrls(string url) =>
        Assert.Throws<ProviderFault>(() =>
            ProviderHttp.OperationUri("https://resource.test/", url)
        );
}
