using FindThatBook.Api.Models.LanguageModels;
using FindThatBook.Api.Prompts;
using FindThatBook.Api.Providers.BookProviders;
using FindThatBook.Api.Providers.LanguageModelProviders;

namespace FindThatBook.Api.Services.BookFinding;

public sealed class LanguageModelBookFinder(
    IBookProvider bookProvider,
    ILanguageModelProvider languageModelProvider,
    ILogger<LanguageModelBookFinder> logger) : IBookFinder
{
    public async Task<IReadOnlyList<RankedBook>> FindAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var searchSession = new BookSearchSession(bookProvider, logger);
        var prompt = new BookFinderPrompt(query, searchSession);

        logger.LogInformation(
            "Finding books with language-model prompt {PromptId} and bounded Open Library tool access.",
            prompt.Id);

        var completion = await languageModelProvider.GenerateAsync(prompt, cancellationToken);
        var rankedBooks = searchSession.Resolve(completion);

        logger.LogInformation(
            "Book finder completed after {SearchCount} Open Library searches and ranked {BookCount} candidates above its cutoff.",
            searchSession.SearchCount,
            rankedBooks.Count);

        return rankedBooks;
    }
}
