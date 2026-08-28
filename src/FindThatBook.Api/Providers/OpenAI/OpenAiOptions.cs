namespace FindThatBook.Api.Providers.OpenAI;

public sealed class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    public string Model { get; init; } = "gpt-5-mini";

    public string? ApiKey { get; init; }
}
