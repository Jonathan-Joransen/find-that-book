using FindThatBook.Api.Prompts;

namespace FindThatBook.Api.Providers;

public interface ILanguageModelProvider
{
    Task<TResponse> GenerateAsync<TResponse>(
        ILanguageModelPrompt<TResponse> prompt,
        CancellationToken cancellationToken = default);
}
