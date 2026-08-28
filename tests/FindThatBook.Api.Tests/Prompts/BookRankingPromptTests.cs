using System.Text.Json;
using FindThatBook.Api.Models;
using FindThatBook.Api.Models.LanguageModels;
using FindThatBook.Api.Prompts;
using Microsoft.Extensions.AI;
using Xunit;

namespace FindThatBook.Api.Tests.Prompts;

public sealed class BookRankingPromptTests
{
    [Fact]
    public void Constructor_IncludesOriginalPromptKeywordsAndCompleteBooks()
    {
        var prompt = new BookRankingPrompt(
            "  a whale and an obsessive captain  ",
            ["voyage", "whaling"],
            [CreateBook("Moby Dick", "Herman Melville")]);

        using var request = JsonDocument.Parse(prompt.UserMessage);
        var root = request.RootElement;

        Assert.Equal("a whale and an obsessive captain", root.GetProperty("initialUserPrompt").GetString());
        Assert.Equal(
            ["voyage", "whaling"],
            root.GetProperty("keywords").EnumerateArray().Select(value => value.GetString()));
        var book = Assert.Single(root.GetProperty("books").EnumerateArray());
        Assert.Equal("Moby Dick", book.GetProperty("title").GetString());
        Assert.Equal("Herman Melville", book.GetProperty("author").GetString());
        Assert.Equal(1851, book.GetProperty("firstPublishYear").GetInt32());
        Assert.False(book.TryGetProperty("score", out _));
        Assert.Equal(new PromptId("book-ranking", 1), prompt.Id);
        Assert.Equal(8_192, prompt.Settings.MaximumOutputTokens);
        Assert.Equal(ReasoningEffort.Low, prompt.Settings.ReasoningEffort);
    }

    [Fact]
    public void Validate_AcceptsOneValidRankingPerBook()
    {
        var prompt = CreatePrompt(bookCount: 2);
        var firstBook = CreateBook("Book 1", "Author 1");
        var secondBook = CreateBook("Book 2", "Author 2");

        prompt.Validate(new BookRankingCompletion(
        [
            new RankedBook(100, "Exact title and author match.", firstBook),
            new RankedBook(0, "No meaningful details match.", secondBook)
        ]));
    }

    [Fact]
    public void Validate_RejectsChangedBookMetadataOrOrdering()
    {
        var prompt = CreatePrompt(bookCount: 2);
        var firstBook = CreateBook("Book 1", "Author 1");

        Assert.Throws<InvalidDataException>(() => prompt.Validate(
            new BookRankingCompletion(
            [
                new RankedBook(90, "Strong match.", firstBook),
                new RankedBook(80, "Another strong match.", firstBook)
            ])));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Validate_RejectsScoreOutsideZeroThroughOneHundred(int score)
    {
        var prompt = CreatePrompt(bookCount: 1);
        var book = CreateBook("Book 1", "Author 1");

        Assert.Throws<InvalidDataException>(() => prompt.Validate(
            new BookRankingCompletion([new RankedBook(score, "A reason.", book)])));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RejectsEmptyReason(string reason)
    {
        var prompt = CreatePrompt(bookCount: 1);
        var book = CreateBook("Book 1", "Author 1");

        Assert.Throws<InvalidDataException>(() => prompt.Validate(
            new BookRankingCompletion([new RankedBook(80, reason, book)])));
    }

    [Fact]
    public void Validate_RejectsReasonAtTwoHundredCharacters()
    {
        var prompt = CreatePrompt(bookCount: 1);
        var book = CreateBook("Book 1", "Author 1");

        Assert.Throws<InvalidDataException>(() => prompt.Validate(
            new BookRankingCompletion([new RankedBook(80, new string('a', 200), book)])));
    }

    private static BookRankingPrompt CreatePrompt(int bookCount) =>
        new(
            "sea story",
            ["ocean"],
            Enumerable.Range(1, bookCount)
                .Select(index => CreateBook($"Book {index}", $"Author {index}"))
                .ToArray());

    private static Book CreateBook(string title, string author) =>
        new(
            title,
            author,
            1851,
            "An opening sentence.",
            $"/works/{title.Replace(" ", string.Empty)}",
            null,
            null,
            null,
            "Open Library ranked this work as relevant to the query.");
}
