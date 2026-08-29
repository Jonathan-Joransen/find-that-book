using FindThatBook.Api.Models;
using FindThatBook.Api.Models.LanguageModels;
using FindThatBook.Api.Prompts;
using FindThatBook.Api.Providers.BookProviders;
using FindThatBook.Api.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FindThatBook.Api.Tests.Prompts;

public sealed class BookFinderPromptTests
{
    [Fact]
    public void Constructor_TrimsQueryVersionsPromptAndProvidesSearchTool()
    {
        var prompt = CreatePrompt(out _);

        Assert.Equal("a whale and an obsessive captain", prompt.UserMessage);
        Assert.Equal(new PromptId("book-finder", 1), prompt.Id);
        Assert.Equal(8_192, prompt.Settings.MaximumOutputTokens);
        Assert.Equal(ReasoningEffort.Low, prompt.Settings.ReasoningEffort);
        Assert.Contains(
            "return only one representative from each group",
            prompt.SystemMessage);
        Assert.Equal(
            "search_open_library",
            Assert.IsAssignableFrom<AIFunction>(Assert.Single(prompt.Tools!)).Name);
    }

    [Fact]
    public async Task SearchTool_NormalizesArgumentsAndReturnsOpaqueCandidates()
    {
        var provider = new RecordingBookProvider(CreateBook("Moby Dick", "/works/OL1W"));
        var session = CreateSession(provider);

        var result = await session.SearchOpenLibraryAsync(
            "  Moby Dick  ",
            "  Herman Melville  ",
            [" Whale ", "whale", "  ", "Captain"]);

        Assert.Equal("Moby Dick", provider.Search?.Title);
        Assert.Equal("Herman Melville", provider.Search?.Author);
        Assert.Equal(["whale", "captain"], provider.Search?.Keywords);
        var candidate = Assert.Single(result.Books);
        Assert.Equal("book-001", candidate.CandidateId);
        Assert.Equal("Moby Dick", candidate.Title);
        Assert.Equal(
            "A sea captain pursues the white whale that maimed him.",
            candidate.Description);
    }

    [Fact]
    public async Task SearchTool_DeduplicatesTheSameBookAcrossSearches()
    {
        var provider = new RecordingBookProvider(CreateBook("Moby Dick", "/works/OL1W"));
        var session = CreateSession(provider);

        var first = await session.SearchOpenLibraryAsync(title: "Moby Dick");
        var second = await session.SearchOpenLibraryAsync(keywords: ["whale"]);

        Assert.Equal(
            Assert.Single(first.Books).CandidateId,
            Assert.Single(second.Books).CandidateId);
    }

    [Fact]
    public async Task SearchTool_RejectsMoreThanThreeSearches()
    {
        var session = CreateSession(new RecordingBookProvider());

        await session.SearchOpenLibraryAsync(keywords: ["one"]);
        await session.SearchOpenLibraryAsync(keywords: ["two"]);
        await session.SearchOpenLibraryAsync(keywords: ["three"]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.SearchOpenLibraryAsync(keywords: ["four"]));
    }

    [Fact]
    public void Validate_RejectsCompletionBeforeAnySearch()
    {
        var prompt = CreatePrompt(out _);

        Assert.Throws<InvalidDataException>(() => prompt.Validate(
            new BookFinderCompletion([])));
    }

    [Fact]
    public async Task ValidateAndResolve_AcceptOnlyCandidatesReturnedByTheTool()
    {
        var book = CreateBook("Moby Dick", "/works/OL1W");
        var prompt = CreatePrompt(out var session, new RecordingBookProvider(book));
        var toolResult = await session.SearchOpenLibraryAsync(title: "Moby Dick");
        var candidateId = Assert.Single(toolResult.Books).CandidateId;
        var completion = new BookFinderCompletion(
        [
            new BookCandidateRanking(candidateId, 98, "  The title and author strongly match.  ")
        ]);

        var normalized = prompt.Normalize(completion);
        prompt.Validate(normalized);
        var resolved = Assert.Single(session.Resolve(normalized));

        Assert.Equal(98, resolved.Score);
        Assert.Equal("The title and author strongly match.", resolved.Reason);
        Assert.Same(book, resolved.Book);
    }

    [Fact]
    public async Task Validate_RejectsUnknownOrDuplicateCandidateIds()
    {
        var prompt = CreatePrompt(out var session, new RecordingBookProvider(CreateBook("Moby Dick", "/works/OL1W")));
        var result = await session.SearchOpenLibraryAsync(title: "Moby Dick");
        var candidateId = Assert.Single(result.Books).CandidateId;

        Assert.Throws<InvalidDataException>(() => prompt.Validate(
            new BookFinderCompletion(
            [
                new BookCandidateRanking("book-999", 90, "Unknown book."),
            ])));
        Assert.Throws<InvalidDataException>(() => prompt.Validate(
            new BookFinderCompletion(
            [
                new BookCandidateRanking(candidateId, 90, "Strong match."),
                new BookCandidateRanking(candidateId, 80, "Duplicate match.")
            ])));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task Validate_RejectsScoreOutsideZeroThroughOneHundred(int score)
    {
        var prompt = CreatePrompt(out var session, new RecordingBookProvider(CreateBook("Moby Dick", "/works/OL1W")));
        var candidateId = Assert.Single(
            (await session.SearchOpenLibraryAsync(title: "Moby Dick")).Books).CandidateId;

        Assert.Throws<InvalidDataException>(() => prompt.Validate(
            new BookFinderCompletion(
            [
                new BookCandidateRanking(candidateId, score, "A reason.")
            ])));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_RejectsEmptyReason(string reason)
    {
        var prompt = CreatePrompt(out var session, new RecordingBookProvider(CreateBook("Moby Dick", "/works/OL1W")));
        var candidateId = Assert.Single(
            (await session.SearchOpenLibraryAsync(title: "Moby Dick")).Books).CandidateId;

        Assert.Throws<InvalidDataException>(() => prompt.Validate(
            new BookFinderCompletion(
            [
                new BookCandidateRanking(candidateId, 80, reason)
            ])));
    }

    private static BookFinderPrompt CreatePrompt(out BookSearchSession session, IBookProvider? provider = null)
    {
        session = CreateSession(provider ?? new RecordingBookProvider());
        return new BookFinderPrompt("  a whale and an obsessive captain  ", session);
    }

    private static BookSearchSession CreateSession(IBookProvider provider) =>
        new(provider, NullLogger.Instance);

    private static Book CreateBook(string title, string? bookKey) =>
        new(
            title,
            "Herman Melville",
            1851,
            "A sea captain pursues the white whale that maimed him.",
            bookKey,
            null,
            null,
            null,
            "Open Library ranked this work as relevant to the query.");

    private sealed class RecordingBookProvider(params Book[] results) : IBookProvider
    {
        public BookSearchQuery? Search { get; private set; }

        public Task<IReadOnlyList<Book>> SearchAsync(
            BookSearchQuery search,
            CancellationToken cancellationToken = default)
        {
            Search = search;
            return Task.FromResult<IReadOnlyList<Book>>(results);
        }
    }
}
