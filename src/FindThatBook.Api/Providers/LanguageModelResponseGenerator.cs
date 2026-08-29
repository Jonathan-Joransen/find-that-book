using FindThatBook.Api.Prompts;
using Microsoft.Extensions.AI;

namespace FindThatBook.Api.Providers;

internal static class LanguageModelResponseGenerator
{
    public static async Task<TResponse> GenerateAsync<TResponse>(
        IChatClient chatClient,
        ILanguageModelPrompt<TResponse> prompt,
        CancellationToken cancellationToken)
    {
        var messages = new ChatMessage[]
        {
            new(ChatRole.System, prompt.SystemMessage),
            new(ChatRole.User, prompt.UserMessage)
        };
        var options = new ChatOptions
        {
            Temperature = prompt.Settings.Temperature,
            MaxOutputTokens = prompt.Settings.MaximumOutputTokens,
            Tools = prompt.Tools,
            AllowMultipleToolCalls = prompt.Tools is { Count: > 0 } ? false : null,
            Reasoning = prompt.Settings.ReasoningEffort is { } effort
                ? new ReasoningOptions { Effort = effort }
                : null
        };

        var response = await chatClient.GetResponseAsync<TResponse>(
            messages,
            options,
            useJsonSchemaResponseFormat: true,
            cancellationToken);

        var normalizedResponse = prompt.Normalize(response.Result);
        prompt.Validate(normalizedResponse);
        return normalizedResponse;
    }
}
