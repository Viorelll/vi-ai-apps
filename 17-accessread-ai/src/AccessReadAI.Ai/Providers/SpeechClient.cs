using System.Diagnostics;
using System.Security;
using System.Text;
using AccessReadAI.Ai.Models;
using AccessReadAI.Contracts;
using Microsoft.Extensions.Options;

namespace AccessReadAI.Ai.Providers;

public sealed class SpeechClient(HttpClient http, IOptions<ProviderOptions> options)
{
    public static string BuildSsml(string text, string language, string voice)
    {
        LanguageCatalog.Validate(language);
        if (!LanguageCatalog.Voices.Values.Contains(voice) || text.Length is 0 or > 3000)
            throw new ApiFault(400, "invalid_voice_or_text_length");
        return $"<speak xmlns=\"http://www.w3.org/2001/10/synthesis\" version=\"1.0\" xml:lang=\"{SecurityElement.Escape(voice[..5])}\"><voice name=\"{SecurityElement.Escape(voice)}\">{SecurityElement.Escape(text)}</voice></speak>";
    }

    public async Task<NarrateResult> NarrateAsync(NarrateRequest input, CancellationToken ct)
    {
        LanguageCatalog.Validate(input.Language);
        var config = options.Value;
        var voice = LanguageCatalog.Voices[input.Language];
        var ssml = BuildSsml(input.Text, input.Language, voice);
        if (config.Demo)
        {
            var temp = Path.GetTempFileName();
            var textFile = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(textFile, input.Text, ct);
                var info = new ProcessStartInfo(config.DemoSpeechExecutable)
                {
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };
                foreach (var arg in new[] { "-v", input.Language, "-w", temp, "-f", textFile })
                    info.ArgumentList.Add(arg);
                using var process =
                    Process.Start(info) ?? throw new ProviderFault("demo_speech_unavailable");
                try
                {
                    await process.WaitForExitAsync(ct);
                }
                catch
                {
                    process.Kill(true);
                    throw;
                }
                if (process.ExitCode != 0)
                    throw new ProviderFault("demo_speech_failed");
                return new(
                    Convert.ToBase64String(await File.ReadAllBytesAsync(temp, ct)),
                    "audio/wav",
                    $"demo-espeak-{input.Language}"
                );
            }
            finally
            {
                File.Delete(temp);
                File.Delete(textFile);
            }
        }
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(new Uri(config.SpeechEndpoint), "cognitiveservices/v1")
        );
        request.Headers.Add("Ocp-Apim-Subscription-Key", config.SpeechKey);
        request.Headers.Add("X-Microsoft-OutputFormat", "riff-16khz-16bit-mono-pcm");
        request.Headers.Add("User-Agent", "AccessReadAI");
        request.Content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml");
        using var response = await http.SendAsync(request, ct);
        await ProviderHttp.EnsureAsync(response, ct);
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        return new(Convert.ToBase64String(bytes), "audio/wav", voice);
    }
}
