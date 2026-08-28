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
    private const int MinimumSearchRankingScore = 60;

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

        var candidates = await bookProvider.SearchAsync(completion, cancellationToken);
        if (candidates.Count == 0)
        {
            return [];
        }

        var rankingPrompt = new BookRankingPrompt(query, completion.Keywords, candidates);
        BookRankingCompletion ranking = await languageModelProvider.GenerateAsync(
            rankingPrompt,
            cancellationToken);

        logger.LogInformation(
            "Ranked {BookCount} Open Library candidates using prompt {PromptId}.",
            candidates.Count,
            rankingPrompt.Id);

        return ranking.RankedBooks
            .Where(book => book.Score > MinimumSearchRankingScore)
            .OrderByDescending(book => book.Score)
            .Select(book => book.Book with { Explanation = book.Reason.Trim() })
            .ToArray();
    }
}
