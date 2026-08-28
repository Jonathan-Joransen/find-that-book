using FindThatBook.Api.Prompts;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace FindThatBook.Api.Providers.OpenAI;

public sealed class OpenAiLanguageModelProvider : ILanguageModelProvider, IDisposable
{
    private readonly OpenAiOptions _options;
    private readonly Lazy<IChatClient> _chatClient;

    public OpenAiLanguageModelProvider(IOptions<OpenAiOptions> options)
    {
        _options = options.Value;
        _chatClient = new Lazy<IChatClient>(CreateChatClient);
    }

    public async Task<TResponse> GenerateAsync<TResponse>(
        ILanguageModelPrompt<TResponse> prompt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await LanguageModelResponseGenerator.GenerateAsync(
                _chatClient.Value,
                prompt,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new LanguageModelException(
                $"OpenAI could not complete prompt '{prompt.Id}'.",
                exception);
        }
    }

    public void Dispose()
    {
        if (_chatClient.IsValueCreated)
        {
            _chatClient.Value.Dispose();
        }
    }

    private IChatClient CreateChatClient()
    {
        var apiKey = _options.ApiKey
            ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "OpenAI requires OpenAI:ApiKey or the OPENAI_API_KEY environment variable.");
        }

        return new ChatClient(_options.Model, apiKey).AsIChatClient();
    }
}
