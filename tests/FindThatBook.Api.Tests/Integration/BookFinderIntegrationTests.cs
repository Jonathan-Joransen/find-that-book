using FindThatBook.Api.Models;
using FindThatBook.Api.Models.LanguageModels;
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

    [GeminiIntegrationTheory]
    [InlineData("tale two cities")]
    [InlineData("uh I think it was tale of two cities maybe charles dikens?")]
    public async Task FindAsync_HandlesPartialTitleAndNoisyMixedEvidence(string input)
    {
        var expectedBook = CreateBook(
            "A Tale of Two Cities",
            "Charles Dickens",
            1859,
            "A novel set in London and Paris before and during the French Revolution.",
            "/works/OL171751W");
        var similarTitle = CreateBook(
            "A Tale of Two Kitties",
            "Dav Pilkey",
            2017,
            "A children's story about two cats.",
            "/works/OL19728163W");
        var sameAuthor = CreateBook(
            "Great Expectations",
            "Charles Dickens",
            1861,
            "Pip recounts his growth and personal development.",
            "/works/OL45804W");
        var bookProvider = new StubBookProvider(expectedBook, similarTitle, sameAuthor);
        using var languageModel = CreateLanguageModel();
        var finder = CreateFinder(bookProvider, languageModel);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));

        var rankings = await finder.FindAsync(input, timeout.Token);

        var bestMatch = Assert.IsType<RankedBook>(rankings.FirstOrDefault());
        output.WriteLine($"Input: {input}");
        output.WriteLine($"Best match: {bestMatch.Book.Title} ({bestMatch.Score}) - {bestMatch.Reason}");
        Assert.Equal(expectedBook.BookKey, bestMatch.Book.BookKey);
        Assert.True(bestMatch.Score > 60);
        Assert.InRange(bookProvider.SearchCount, 1, BookSearchSession.MaximumSearches);
    }

    [GeminiIntegrationFact]
    public async Task FindAsync_ReturnsSeveralPlausibleBooksForAmbiguousAuthorOnlyQuery()
    {
        const string input = "dickens";
        var dickensBooks = new[]
        {
            CreateBook(
                "Great Expectations",
                "Charles Dickens",
                1861,
                "Pip recounts his growth and personal development.",
                "/works/OL45804W"),
            CreateBook(
                "Oliver Twist",
                "Charles Dickens",
                1838,
                "An orphan encounters poverty and crime in London.",
                "/works/OL473028W"),
            CreateBook(
                "A Tale of Two Cities",
                "Charles Dickens",
                1859,
                "A novel set in London and Paris during the French Revolution.",
                "/works/OL171751W")
        };
        var unrelatedBook = CreateBook(
            "Pride and Prejudice",
            "Jane Austen",
            1813,
            "Elizabeth Bennet navigates manners and marriage.",
            "/works/OL66554W");
        var bookProvider = new StubBookProvider([.. dickensBooks, unrelatedBook]);
        using var languageModel = CreateLanguageModel();
        var finder = CreateFinder(bookProvider, languageModel);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));

        var rankings = await finder.FindAsync(input, timeout.Token);

        Assert.True(rankings.Count >= 2, "An ambiguous author-only query should return several plausible books.");
        Assert.All(rankings, ranking => Assert.Contains(
            "Dickens",
            ranking.Book.Author,
            StringComparison.OrdinalIgnoreCase));
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

    private static LanguageModelBookFinder CreateFinder(
        IBookProvider bookProvider,
        ILanguageModelProvider languageModel) =>
        new(
            bookProvider,
            languageModel,
            NullLogger<LanguageModelBookFinder>.Instance);

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
