namespace FindThatBook.Api.Providers.Gemini;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string Model { get; init; } = "gemini-3.7-flash";

    public string? ApiKey { get; init; }
}
