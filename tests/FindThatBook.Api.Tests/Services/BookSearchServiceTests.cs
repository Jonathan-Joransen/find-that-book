using FindThatBook.Api.Models;
using FindThatBook.Api.Models.LanguageModels;
using FindThatBook.Api.Providers.LanguageModelProviders;
using FindThatBook.Api.Services.BookFinding;
using FindThatBook.Api.Services.BookSearch;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FindThatBook.Api.Tests.Services;

public sealed class BookSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_ReturnsAllBooksAboveCutoffInScoreOrder()
    {
        var finder = new StubBookFinder(
            new RankedBook(42, "Only broad details overlap.", CreateBook("Weak match")),
            new RankedBook(98, "  The title and author strongly match.  ", CreateBook("Best match")),
            new RankedBook(60, "The evidence is at the cutoff.", CreateBook("Cutoff match")),
            new RankedBook(61, "The setting and plot details plausibly match.", CreateBook("Good match")));
        var service = CreateService(finder);

        var results = await service.SearchAsync("  a whale and an obsessive captain  ");

        Assert.Collection(
            results,
            book =>
            {
                Assert.Equal("Best match", book.Title);
                Assert.Equal(98, book.Score);
                Assert.Equal("The title and author strongly match.", book.Explanation);
            },
            book =>
            {
                Assert.Equal("Good match", book.Title);
                Assert.Equal(61, book.Score);
            });
        Assert.Equal("a whale and an obsessive captain", finder.Query);
    }

    [Fact]
    public async Task SearchAsync_CapsResultsAtTwelveHighestScores()
    {
        var rankings = Enumerable.Range(1, 15)
            .Select(index => new RankedBook(
                70 + index,
                $"Reason {index}.",
                CreateBook($"Book {index}")))
            .ToArray();
        var service = CreateService(new StubBookFinder(rankings));

        var results = await service.SearchAsync("many plausible books");

        Assert.Equal(12, results.Count);
        Assert.Equal("Book 15", results[0].Title);
        Assert.Equal("Book 4", results[^1].Title);
        Assert.Equal(
            Enumerable.Range(74, 12).Reverse().Select(score => (int?)score),
            results.Select(book => book.Score));
    }

    [Fact]
    public async Task SearchAsync_PropagatesLanguageModelFailure()
    {
        var service = CreateService(new ThrowingBookFinder());

        await Assert.ThrowsAsync<LanguageModelException>(
            () => service.SearchAsync("original query"));
    }

    private static BookSearchService CreateService(IBookFinder finder) =>
        new(finder, NullLogger<BookSearchService>.Instance);

    private static Book CreateBook(string title) =>
        new(
            title,
            "An author",
            null,
            string.Empty,
            $"/works/{title.Replace(" ", string.Empty)}",
            null,
            null,
            null,
            "Open Library ranked this work as relevant to the query.");

    private sealed class StubBookFinder(params RankedBook[] results) : IBookFinder
    {
        public string? Query { get; private set; }

        public Task<IReadOnlyList<RankedBook>> FindAsync(
            string query,
            CancellationToken cancellationToken = default)
        {
            Query = query;
            return Task.FromResult<IReadOnlyList<RankedBook>>(results);
        }
    }

    private sealed class ThrowingBookFinder : IBookFinder
    {
        public Task<IReadOnlyList<RankedBook>> FindAsync(
            string query,
            CancellationToken cancellationToken = default) =>
            throw new LanguageModelException(
                "Language model unavailable.",
                new HttpRequestException("Provider request failed."));
    }
}
