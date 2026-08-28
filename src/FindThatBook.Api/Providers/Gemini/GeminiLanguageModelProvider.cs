using FindThatBook.Api.Prompts;
using Google.GenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace FindThatBook.Api.Providers.Gemini;

public sealed class GeminiLanguageModelProvider : ILanguageModelProvider, IDisposable
{
    private readonly GeminiOptions _options;
    private readonly Lazy<IChatClient> _chatClient;

    public GeminiLanguageModelProvider(IOptions<GeminiOptions> options)
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
                $"Gemini could not complete prompt '{prompt.Id}'.",
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
            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Gemini requires Gemini:ApiKey or the GEMINI_API_KEY environment variable.");
        }

        return new Client(apiKey: apiKey).AsIChatClient(_options.Model);
    }
}
