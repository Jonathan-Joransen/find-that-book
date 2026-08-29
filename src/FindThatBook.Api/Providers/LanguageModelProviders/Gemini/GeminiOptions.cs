namespace FindThatBook.Api.Providers.LanguageModelProviders.Gemini;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string Model { get; init; } = "gemini-3.7-flash";

    public string? ApiKey { get; init; }
}
