using FindThatBook.Api.Models;
using FindThatBook.Api.Models.LanguageModels;
using FindThatBook.Api.Prompts;
using FindThatBook.Api.Providers;
using Microsoft.Extensions.Options;

namespace FindThatBook.Api.Services;

public sealed class BookSearchService(
    IBookProvider bookProvider,
    ILanguageModelProvider languageModelProvider,
    IOptions<LanguageModelOptions> languageModelOptions,
    ILogger<BookSearchService> logger) : IBookSearchService
{
    private readonly LanguageModelOptions _languageModelOptions = languageModelOptions.Value;

    public async Task<IReadOnlyList<Book>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var searchQuery = query.Trim();

        if (_languageModelOptions.Enabled)
        {
            var prompt = new BookSearchPrompt(searchQuery);
            BookSearchCompletion completion = await languageModelProvider.GenerateAsync(
                prompt,
                cancellationToken);

            searchQuery = completion.SearchQuery.Trim();
            logger.LogInformation(
                "Refined book search with Gemini using prompt {PromptId}.",
                prompt.Id);
        }

        return await bookProvider.SearchAsync(searchQuery, cancellationToken);
    }
}
