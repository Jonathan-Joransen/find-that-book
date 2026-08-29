using System.Diagnostics;
using System.Text.Json;
using FindThatBook.Api.Prompts;
using FindThatBook.Api.Providers.LanguageModelProviders;
using FindThatBook.Api.Services;
using Google.GenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace FindThatBook.Api.Providers.LanguageModelProviders.Gemini;

public sealed class GeminiLanguageModelProvider : ILanguageModelProvider, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiLanguageModelProvider> _logger;
    private readonly Lazy<IChatClient> _chatClient;

    public GeminiLanguageModelProvider(
        IOptions<GeminiOptions> options,
        ILogger<GeminiLanguageModelProvider> logger)
    {
        _options = options.Value;
        _logger = logger;
        _chatClient = new Lazy<IChatClient>(CreateChatClient);
    }

    public async Task<TResponse> GenerateAsync<TResponse>(
        ILanguageModelPrompt<TResponse> prompt,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();

        _logger.LogInformation(
            "Sending prompt {PromptId} to Gemini model {Model}.",
            prompt.Id,
            _options.Model);

        try
        {
            var result = await LanguageModelResponseGenerator.GenerateAsync(
                _chatClient.Value,
                prompt,
                cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Gemini returned a response for prompt {PromptId} in {ElapsedMilliseconds} ms: {Response}",
                    prompt.Id,
                    Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                    JsonSerializer.Serialize(result, JsonOptions));
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Gemini failed prompt {PromptId} after {ElapsedMilliseconds} ms.",
                prompt.Id,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

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

        var geminiClient = new Client(apiKey: apiKey).AsIChatClient(_options.Model);

        return new FunctionInvokingChatClient(
            geminiClient,
            loggerFactory: null,
            functionInvocationServices: null)
        {
            AllowConcurrentInvocation = false,
            IncludeDetailedErrors = false,
            MaximumConsecutiveErrorsPerRequest = 0,
            MaximumIterationsPerRequest = BookSearchSession.MaximumSearches + 1,
            TerminateOnUnknownCalls = true
        };
    }
}
