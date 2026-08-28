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
            MaxOutputTokens = prompt.Settings.MaximumOutputTokens
        };

        var response = await chatClient.GetResponseAsync<TResponse>(
            messages,
            options,
            useJsonSchemaResponseFormat: true,
            cancellationToken);

        prompt.Validate(response.Result);
        return response.Result;
    }
}
