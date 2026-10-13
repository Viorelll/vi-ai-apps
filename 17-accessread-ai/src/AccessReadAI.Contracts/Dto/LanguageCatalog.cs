namespace AccessReadAI.Contracts;

public static class LanguageCatalog
{
    public static readonly IReadOnlyDictionary<string, string> Voices = new Dictionary<
        string,
        string
    >
    {
        ["ro"] = "ro-RO-AlinaNeural",
        ["en"] = "en-US-JennyNeural",
        ["fr"] = "fr-FR-DeniseNeural",
        ["de"] = "de-DE-KatjaNeural",
        ["es"] = "es-ES-ElviraNeural",
    };

    public static void Validate(string? language)
    {
        if (language is null || !Voices.ContainsKey(language))
            throw new ApiFault(400, "unsupported_language");
    }
}
