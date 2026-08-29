using FindThatBook.Api.Models;

namespace FindThatBook.Api.Services;

public sealed class BookSearchService(
    IBookFinder bookFinder,
    ILogger<BookSearchService> logger) : IBookSearchService
{
    private const int MinimumSearchRankingScore = 60;
    private const int MaximumSearchResults = 12;

    public async Task<IReadOnlyList<Book>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        logger.LogInformation("Starting book search for query {Query}.", query.Trim());
        var ranking = await bookFinder.FindAsync(query.Trim(), cancellationToken);
        var results = ranking
            .Where(book => book.Score > MinimumSearchRankingScore)
            .OrderByDescending(book => book.Score)
            .Take(MaximumSearchResults)
            .Select(book => book.Book with
            {
                Explanation = book.Reason.Trim(),
                Score = book.Score
            })
            .ToArray();

        logger.LogInformation(
            "Book search complete. Returning {ResultCount} of {BookCount} ranked candidates above score {MinimumScore}, capped at {MaximumResults}.",
            results.Length,
            ranking.Count,
            MinimumSearchRankingScore,
            MaximumSearchResults);

        return results;
    }
}
