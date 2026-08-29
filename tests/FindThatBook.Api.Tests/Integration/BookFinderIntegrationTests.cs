using FindThatBook.Api.Models;
using FindThatBook.Api.Models.LanguageModels;
using FindThatBook.Api.Providers.BookProviders;
using FindThatBook.Api.Providers.LanguageModelProviders;
using FindThatBook.Api.Providers.LanguageModelProviders.Gemini;
using FindThatBook.Api.Services.BookFinding;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace FindThatBook.Api.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class BookFinderIntegrationTests(ITestOutputHelper output)
{
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

    [GeminiIntegrationTheory]
    // Shepard wrote the memoir, not Pooh.
    [InlineData(
        "books by E. H. Shepard",
        "Drawn from Memory",
        "The Tao of Pooh")]
    // Lee wrote the sketchbook, not Troy.
    [InlineData(
        "books written by Alan Lee",
        "The Lord of the Rings Sketchbook",
        "Black Ships Before Troy")]
    // GrandPre wrote Cleonardo, not Dragon's Guide.
    [InlineData(
        "books written by Mary GrandPre",
        "Cleonardo, the little inventor",
        "A Dragon's Guide To The Care And Feeding Of Humans")]
    // Tenniel created Cartoons, not Haunted Man.
    [InlineData(
        "books by John Tenniel",
        "Cartoons (from Punch)",
        "The Haunted Man and the Ghost's Bargain")]
    public async Task FindAsync_PrefersPrimaryAuthorMatchesOverContributorOnlyMatches(
        string input,
        string expectedTitle,
        string excludedTitle)
    {
        var primaryAuthorMatch = CreatePrimaryAuthorMatch(expectedTitle);
        var contributorOnlyMatch = CreateContributorOnlyMatch(excludedTitle);
        var bookProvider = new StubBookProvider(contributorOnlyMatch, primaryAuthorMatch);
        using var languageModel = CreateLanguageModel();
        var finder = CreateFinder(bookProvider, languageModel);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));

        var rankings = await finder.FindAsync(input, timeout.Token);

        output.WriteLine($"Input: {input}");
        foreach (var ranking in rankings)
        {
            output.WriteLine(
                $"Match: {ranking.Book.Title} by {ranking.Book.Author} ({ranking.Score}) - {ranking.Reason}");
        }

        var bestMatch = Assert.IsType<RankedBook>(rankings.FirstOrDefault());
        Assert.Equal(primaryAuthorMatch.BookKey, bestMatch.Book.BookKey);

        var contributorOnlyRanking = rankings.FirstOrDefault(ranking => string.Equals(
            ranking.Book.Title,
            excludedTitle,
            StringComparison.OrdinalIgnoreCase));
        if (contributorOnlyRanking is not null)
        {
            Assert.True(
                bestMatch.Score > contributorOnlyRanking.Score,
                $"Expected primary-author match '{bestMatch.Book.Title}' to outrank contributor-only match '{contributorOnlyRanking.Book.Title}'.");
        }

        Assert.InRange(bookProvider.SearchCount, 1, BookSearchSession.MaximumSearches);
    }

    private static Book CreatePrimaryAuthorMatch(string title) =>
        title switch
        {
            "Drawn from Memory" => CreateBook(
                title,
                "Ernest H. Shepard",
                1957,
                "Ernest H. Shepard's autobiographical memoir.",
                "/works/OL3017266W"),
            "The Lord of the Rings Sketchbook" => CreateBook(
                title,
                "Alan Lee",
                2005,
                "Alan Lee presents his sketches and account of designing Middle-earth.",
                "/works/OL5256894W"),
            "Cleonardo, the little inventor" => CreateBook(
                title,
                "Mary GrandPre",
                2016,
                "Mary GrandPre's story about a young inventor.",
                "/works/OL20033888W"),
            "Cartoons (from Punch)" => CreateBook(
                title,
                "John Tenniel",
                1863,
                "A collection of John Tenniel's Punch cartoons.",
                "/works/OL8251194W"),
            _ => throw new ArgumentOutOfRangeException(nameof(title), title, null)
        };

    private static Book CreateContributorOnlyMatch(string title) =>
        title switch
        {
            "The Tao of Pooh" => CreateBook(
                title,
                "Benjamin Hoff, Ernest H. Shepard, A. A. Milne",
                1982,
                "Benjamin Hoff explains Taoist philosophy through Winnie-the-Pooh.",
                "/works/OL3913006W"),
            "Black Ships Before Troy" => CreateBook(
                title,
                "Rosemary Sutcliff, Alan Lee, Manuel Otero",
                1967,
                "A retelling of the Trojan War and the destruction of Troy.",
                "/works/OL1417812W"),
            "A Dragon's Guide To The Care And Feeding Of Humans" => CreateBook(
                title,
                "Laurence Yep, Joanne Ryder, Mary GrandPre",
                2015,
                "A dragon and her human face magical mishaps together.",
                "/works/OL17828715W"),
            "The Haunted Man and the Ghost's Bargain" => CreateBook(
                title,
                "Charles Dickens, John Tenniel, Frank Stone",
                1848,
                "A haunted professor is offered escape from his painful memories.",
                "/works/OL14869114W"),
            _ => throw new ArgumentOutOfRangeException(nameof(title), title, null)
        };

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
