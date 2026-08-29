using FindThatBook.Api.Models;
using FindThatBook.Api.Providers;
using FindThatBook.Api.Providers.Gemini;
using FindThatBook.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace FindThatBook.Api.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class BookFinderIntegrationTests(ITestOutputHelper output)
{
    [GeminiIntegrationTheory]
    [InlineData("a whale and an obsessive captain", "Moby-Dick")]
    [InlineData("a doctor creates a monster from dead bodies", "Frankenstein")]
    public async Task FindAsync_UsesSearchToolAndReturnsOnlyStrongMatches(
        string input,
        string expectedTitle)
    {
        var expectedBook = expectedTitle == "Moby-Dick"
            ? CreateBook(
                "Moby-Dick",
                "Herman Melville",
                1851,
                "Captain Ahab pursues a great white whale.",
                "/works/OL102749W")
            : CreateBook(
                "Frankenstein",
                "Mary Shelley",
                1818,
                "A scientist creates a living being from dead body parts.",
                "/works/OL450063W");
        var unrelatedBook = CreateBook(
            "Pride and Prejudice",
            "Jane Austen",
            1813,
            "Elizabeth Bennet navigates manners, upbringing, and marriage.",
            "/works/OL66554W");
        var bookProvider = new StubBookProvider(expectedBook, unrelatedBook);
        using var languageModel = CreateLanguageModel();
        var finder = new LanguageModelBookFinder(
            bookProvider,
            languageModel,
            NullLogger<LanguageModelBookFinder>.Instance);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));

        var rankings = await finder.FindAsync(input, timeout.Token);

        var match = Assert.Single(rankings);
        output.WriteLine($"Input: {input}");
        output.WriteLine($"Tool searches: {bookProvider.SearchCount}");
        output.WriteLine($"Match: {match.Book.Title} ({match.Score}) - {match.Reason}");
        Assert.Equal(expectedTitle, match.Book.Title);
        Assert.True(match.Score > 60);
        Assert.InRange(bookProvider.SearchCount, 1, BookSearchSession.MaximumSearches);
    }

    private static Book CreateBook(
        string title,
        string author,
        int firstPublishYear,
        string description,
        string bookKey) =>
        new(
            title,
            author,
            firstPublishYear,
            description,
            bookKey,
            null,
            null,
            null,
            "Open Library ranked this work as relevant to the query.");

    private static GeminiLanguageModelProvider CreateLanguageModel() =>
        new(
            Options.Create(GeminiIntegrationTestConfiguration.GetOptions()),
            NullLogger<GeminiLanguageModelProvider>.Instance);

    private sealed class StubBookProvider(params Book[] books) : IBookProvider
    {
        public int SearchCount { get; private set; }

        public Task<IReadOnlyList<Book>> SearchAsync(
            BookSearchQuery search,
            CancellationToken cancellationToken = default)
        {
            SearchCount++;
            return Task.FromResult<IReadOnlyList<Book>>(books);
        }
    }
}
