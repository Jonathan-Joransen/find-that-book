namespace FindThatBook.Api.Providers;

public sealed class LanguageModelOptions
{
    public const string SectionName = "LanguageModel";

    public bool Enabled { get; init; }

    public string Provider { get; init; } = "Gemini";
}
