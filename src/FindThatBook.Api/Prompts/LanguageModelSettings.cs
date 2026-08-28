namespace FindThatBook.Api.Prompts;

public sealed record LanguageModelSettings(
    float Temperature,
    int MaximumOutputTokens);
