using FindThatBook.Api.Models;
using FindThatBook.Api.Models.LanguageModels;
using FindThatBook.Api.Prompts;
using FindThatBook.Api.Providers;

namespace FindThatBook.Api.Services;

public sealed class BookSearchService(
    IBookProvider bookProvider,
    ILanguageModelProvider languageModelProvider,
    ILogger<BookSearchService> logger) : IBookSearchService
{
    public async Task<IReadOnlyList<Book>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var prompt = new BookSearchPrompt(query);
        BookSearchCompletion completion = await languageModelProvider.GenerateAsync(
            prompt,
            cancellationToken);

        logger.LogInformation(
            "Refined book search using prompt {PromptId}.",
            prompt.Id);

        return await bookProvider.SearchAsync(completion, cancellationToken);
    }
}
