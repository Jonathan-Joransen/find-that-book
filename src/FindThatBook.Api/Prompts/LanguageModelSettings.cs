using Microsoft.Extensions.AI;

namespace FindThatBook.Api.Prompts;

public sealed record LanguageModelSettings(
    float Temperature,
    int MaximumOutputTokens,
    ReasoningEffort? ReasoningEffort = null);
