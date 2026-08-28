using FindThatBook.Api.Models;
using FindThatBook.Api.Models.LanguageModels;
using FindThatBook.Api.Prompts;
using FindThatBook.Api.Providers;
using FindThatBook.Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FindThatBook.Api.Tests.Services;

public sealed class BookSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_UsesLanguageModelQuery()
    {
        var books = new RecordingBookProvider();
        var languageModel = new StubLanguageModelProvider(
            new BookSearchCompletion("Moby Dick", "Herman Melville", ["voyage", "whaling"]));
        var service = CreateService(books, languageModel);

        await service.SearchAsync("a whale and an obsessive captain");

        Assert.Equal("Moby Dick", books.Search?.Title);
        Assert.Equal("Herman Melville", books.Search?.Author);
        Assert.Equal(["voyage", "whaling"], books.Search?.Keywords);
        Assert.True(languageModel.WasCalled);
        Assert.Equal(new PromptId("book-search", 5), languageModel.PromptId);
    }

    [Fact]
    public async Task SearchAsync_DoesNotSearchBooksWhenLanguageModelFails()
    {
        var books = new RecordingBookProvider();
        var service = CreateService(books, new ThrowingLanguageModelProvider());

        await Assert.ThrowsAsync<LanguageModelException>(
            () => service.SearchAsync("  original query  "));

        Assert.Null(books.Search);
    }

    [Fact]
    public async Task SearchAsync_RanksFiltersAndOrdersOpenLibraryCandidates()
    {
        var provider = new RecordingBookProvider(
            CreateBook("Weak match"),
            CreateBook("Best match"),
            CreateBook("Borderline match"),
            CreateBook("Good match"));
        var rankedBooks = provider.Results
            .Select((book, index) => new RankedBook(
                new[] { 42, 98, 60, 61 }[index],
                new[]
                {
                    "Only broad details overlap.",
                    "  The title and author strongly match the request.  ",
                    "The evidence is too weak.",
                    "The setting and plot details plausibly match."
                }[index],
                book))
            .ToList();
        var languageModel = new StubLanguageModelProvider(
            new BookSearchCompletion(null, null, ["captain", "whale"]),
            new BookRankingCompletion(rankedBooks));
        var service = CreateService(provider, languageModel);

        var results = await service.SearchAsync("a whale and an obsessive captain");

        Assert.Collection(
            results,
            book =>
            {
                Assert.Equal("Best match", book.Title);
                Assert.Equal(
                    "The title and author strongly match the request.",
                    book.Explanation);
            },
            book =>
            {
                Assert.Equal("Good match", book.Title);
                Assert.Equal(
                    "The setting and plot details plausibly match.",
                    book.Explanation);
            });
        Assert.Equal(
            [new PromptId("book-search", 5), new PromptId("book-ranking", 1)],
            languageModel.PromptIds);
    }

    [Fact]
    public async Task SearchAsync_DoesNotRankWhenOpenLibraryReturnsNoCandidates()
    {
        var languageModel = new StubLanguageModelProvider(
            new BookSearchCompletion(null, null, ["details", "unknown"]));
        var service = CreateService(new RecordingBookProvider(), languageModel);

        var results = await service.SearchAsync("unknown book");

        Assert.Empty(results);
        Assert.Equal([new PromptId("book-search", 5)], languageModel.PromptIds);
    }

    [Fact]
    public async Task SearchAsync_LogsEachSearchStepAtInformationLevel()
    {
        var provider = new RecordingBookProvider(CreateBook("Moby Dick"));
        var languageModel = new StubLanguageModelProvider(
            new BookSearchCompletion("Moby Dick", "Herman Melville", ["voyage", "whaling"]),
            new BookRankingCompletion(
            [
                new RankedBook(95, "The title and author match.", provider.Results[0])
            ]));
        var logger = new RecordingLogger<BookSearchService>();
        var service = CreateService(provider, languageModel, logger);

        await service.SearchAsync("a whale and an obsessive captain");

        Assert.All(logger.Entries, entry => Assert.Equal(LogLevel.Information, entry.Level));
        Assert.Contains(logger.Entries, entry => entry.Message.StartsWith("Step 1:"));
        Assert.Contains(logger.Entries, entry => entry.Message.StartsWith("Step 1 complete."));
        Assert.Contains(logger.Entries, entry => entry.Message.StartsWith("Step 2:"));
        Assert.Contains(logger.Entries, entry => entry.Message.StartsWith("Step 2 complete."));
        Assert.Contains(logger.Entries, entry => entry.Message.StartsWith("Step 3:"));
        Assert.Contains(logger.Entries, entry => entry.Message.StartsWith("Step 3 complete."));
    }

    private static BookSearchService CreateService(
        IBookProvider books,
        ILanguageModelProvider languageModel,
        ILogger<BookSearchService>? logger = null) =>
        new(
            books,
            languageModel,
            logger ?? NullLogger<BookSearchService>.Instance);

    private static Book CreateBook(string title) =>
        new(
            title,
            "An author",
            null,
            string.Empty,
            null,
            null,
            null,
            null,
            "Open Library ranked this work as relevant to the query.");

    private sealed class RecordingBookProvider(params Book[] results) : IBookProvider
    {
        public BookSearchCompletion? Search { get; private set; }

        public IReadOnlyList<Book> Results => results;

        public Task<IReadOnlyList<Book>> SearchAsync(
            BookSearchCompletion search,
            CancellationToken cancellationToken = default)
        {
            Search = search;
            return Task.FromResult<IReadOnlyList<Book>>(results);
        }
    }

    private sealed class StubLanguageModelProvider(params object[] responses)
        : ILanguageModelProvider
    {
        private int _responseIndex;

        public bool WasCalled { get; private set; }

        public PromptId? PromptId { get; private set; }

        public List<PromptId> PromptIds { get; } = [];

        public Task<TResponse> GenerateAsync<TResponse>(
            ILanguageModelPrompt<TResponse> prompt,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            PromptId = prompt.Id;
            PromptIds.Add(prompt.Id);
            return Task.FromResult((TResponse)responses[_responseIndex++]);
        }
    }

    private sealed class ThrowingLanguageModelProvider : ILanguageModelProvider
    {
        public Task<TResponse> GenerateAsync<TResponse>(
            ILanguageModelPrompt<TResponse> prompt,
            CancellationToken cancellationToken = default) =>
            throw new LanguageModelException(
                "Language model unavailable.",
                new HttpRequestException("Provider request failed."));
    }
}
