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

        logger.LogInformation("Starting book search for query {Query}.", query.Trim());

        var prompt = new BookSearchPrompt(query);
        logger.LogInformation(
            "Step 1: interpreting the query with language-model prompt {PromptId}.",
            prompt.Id);

        BookSearchCompletion completion = await languageModelProvider.GenerateAsync(
            prompt,
            cancellationToken);

        logger.LogInformation(
            "Step 1 complete. Extracted title {Title}, author {Author}, and keywords {Keywords}.",
            completion.Title,
            completion.Author,
            completion.Keywords);

        logger.LogInformation("Step 2: searching Open Library with the extracted evidence.");
        var candidates = await bookProvider.SearchAsync(completion, cancellationToken);
        logger.LogInformation(
            "Step 2 complete. Open Library returned {BookCount} candidates.",
            candidates.Count);

        if (candidates.Count == 0)
        {
            logger.LogInformation("Book search finished because Open Library returned no candidates.");
            return [];
        }

        var rankingPrompt = new BookRankingPrompt(query, completion.Keywords, candidates);
        logger.LogInformation(
            "Step 3: ranking {BookCount} candidates with language-model prompt {PromptId}.",
            candidates.Count,
            rankingPrompt.Id);

        BookRankingCompletion ranking = await languageModelProvider.GenerateAsync(
            rankingPrompt,
            cancellationToken);

        var results = ranking.RankedBooks
            .Where(book => book.Score > MinimumSearchRankingScore)
            .OrderByDescending(book => book.Score)
            .Select(book => book.Book with { Explanation = book.Reason.Trim() })
            .ToArray();

        logger.LogInformation(
            "Step 3 complete. Returning {ResultCount} of {BookCount} ranked candidates above score {MinimumScore}.",
            results.Length,
            candidates.Count,
            MinimumSearchRankingScore);

        return results;
    }
}
