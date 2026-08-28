using FindThatBook.Api.Models;
using FindThatBook.Api.Models.LanguageModels;
using FindThatBook.Api.Prompts;
using FindThatBook.Api.Providers.Gemini;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace FindThatBook.Api.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class BookRankingIntegrationTests(ITestOutputHelper output)
{
    private const int MinimumSearchRankingScore = 60;

    [GeminiIntegrationTheory]
    [InlineData("dickens", null, "Great Expectations", "Charles Dickens", 1861, "Pip recounts his childhood and unexpected fortune.")]
    [InlineData("prince of wales", null, "Prince of Wales", "Jonathan Dimbleby", 1994, "A biography of Charles, Prince of Wales.")]
    [InlineData("moby whale", "whale", "Moby-Dick", "Herman Melville", 1851, "Captain Ahab pursues a great white whale.")]
    public async Task GenerateAsync_ScoresExpectedMatchesAboveSearchThreshold(
        string input,
        string? keywords,
        string title,
        string author,
        int firstPublishYear,
        string description)
    {
        var rankedBook = await RankAsync(
            input,
            keywords,
            CreateBook(title, author, firstPublishYear, description));

        Assert.True(
            rankedBook.Score > MinimumSearchRankingScore,
            $"Expected a score above {MinimumSearchRankingScore}, but received {rankedBook.Score}: {rankedBook.Reason}");
    }

    [GeminiIntegrationTheory]
    [InlineData("dickens", null, "Dune", "Frank Herbert", 1965, "A noble family struggles for control of a desert planet.")]
    [InlineData("prince of wales", null, "Silent Spring", "Rachel Carson", 1962, "An examination of the environmental effects of pesticides.")]
    [InlineData("moby whale", "whale", "Pride and Prejudice", "Jane Austen", 1813, "Elizabeth Bennet navigates manners, upbringing, and marriage.")]
    public async Task GenerateAsync_ScoresUnrelatedBooksAtOrBelowSearchThreshold(
        string input,
        string? keywords,
        string title,
        string author,
        int firstPublishYear,
        string description)
    {
        var rankedBook = await RankAsync(
            input,
            keywords,
            CreateBook(title, author, firstPublishYear, description));

        Assert.True(
            rankedBook.Score <= MinimumSearchRankingScore,
            $"Expected a score at or below {MinimumSearchRankingScore}, but received {rankedBook.Score}: {rankedBook.Reason}");
    }

    private async Task<RankedBook> RankAsync(
        string input,
        string? keywords,
        Book book)
    {
        using var languageModel = CreateLanguageModel();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var response = await languageModel.GenerateAsync(
            new BookRankingPrompt(input, keywords, [book]),
            timeout.Token);
        var rankedBook = Assert.Single(response.RankedBooks);

        output.WriteLine($"Input: {input}");
        output.WriteLine($"Book: {book.Title} by {book.Author}");
        output.WriteLine($"Score: {rankedBook.Score}");
        output.WriteLine($"Reason: {rankedBook.Reason}");

        return rankedBook;
    }

    private static Book CreateBook(
        string title,
        string author,
        int firstPublishYear,
        string description) =>
        new(
            title,
            author,
            firstPublishYear,
            description,
            $"/works/{title.Replace(" ", string.Empty)}",
            null,
            null,
            null,
            "Open Library ranked this work as relevant to the query.");

    private static GeminiLanguageModelProvider CreateLanguageModel()
        => new(Options.Create(GeminiIntegrationTestConfiguration.GetOptions()));
}
