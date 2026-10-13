using AccessReadAI.Contracts;

namespace AccessReadAI.Ai.Models;

public sealed class ProviderOptions
{
    public bool Demo { get; set; } = true;
    public string FoundryEndpoint { get; set; } = "https://RESOURCE.openai.azure.com/openai/v1/";
    public string FoundryKey { get; set; } = "replace-key";
    public string Deployment { get; set; } = "accessreadai-chat";
    public string LayoutEndpoint { get; set; } = "https://RESOURCE.cognitiveservices.azure.com/";
    public string LayoutKey { get; set; } = "replace-key";
    public string TranslatorEndpoint { get; set; } =
        "https://api.cognitive.microsofttranslator.com/";
    public string TranslatorKey { get; set; } = "replace-key";
    public string TranslatorRegion { get; set; } = "replace-region";
    public string SpeechEndpoint { get; set; } = "https://REGION.tts.speech.microsoft.com/";
    public string SpeechKey { get; set; } = "replace-key";
    public string DemoSpeechExecutable { get; set; } = "espeak-ng";

    public bool Valid() =>
        Demo
        || new[] { FoundryEndpoint, LayoutEndpoint, TranslatorEndpoint, SpeechEndpoint }.All(x =>
            Uri.TryCreate(x, UriKind.Absolute, out var u)
            && u.Scheme == "https"
            && !x.Contains("RESOURCE")
            && !x.Contains("REGION")
        )
            && new[]
            {
                FoundryKey,
                LayoutKey,
                TranslatorKey,
                SpeechKey,
                TranslatorRegion,
                Deployment,
            }.All(x => !string.IsNullOrWhiteSpace(x) && !x.StartsWith("replace-"));
}
