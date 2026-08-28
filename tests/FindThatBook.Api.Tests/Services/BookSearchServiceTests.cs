using FindThatBook.Api.Models;
using FindThatBook.Api.Models.LanguageModels;
using FindThatBook.Api.Prompts;
using FindThatBook.Api.Providers;
using FindThatBook.Api.Services;
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
            new BookSearchCompletion("Moby Dick", "Herman Melville", "whaling voyage"));
        var service = CreateService(books, languageModel);

        await service.SearchAsync("a whale and an obsessive captain");

        Assert.Equal(
            new BookSearchCompletion("Moby Dick", "Herman Melville", "whaling voyage"),
            books.Search);
        Assert.True(languageModel.WasCalled);
        Assert.Equal(new PromptId("book-search", 3), languageModel.PromptId);
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

    private static BookSearchService CreateService(
        IBookProvider books,
        ILanguageModelProvider languageModel) =>
        new(
            books,
            languageModel,
            NullLogger<BookSearchService>.Instance);

    private sealed class RecordingBookProvider : IBookProvider
    {
        public BookSearchCompletion? Search { get; private set; }

        public Task<IReadOnlyList<Book>> SearchAsync(
            BookSearchCompletion search,
            CancellationToken cancellationToken = default)
        {
            Search = search;
            return Task.FromResult<IReadOnlyList<Book>>([]);
        }
    }

    private sealed class StubLanguageModelProvider(object response)
        : ILanguageModelProvider
    {
        public bool WasCalled { get; private set; }

        public PromptId? PromptId { get; private set; }

        public Task<TResponse> GenerateAsync<TResponse>(
            ILanguageModelPrompt<TResponse> prompt,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            PromptId = prompt.Id;
            return Task.FromResult((TResponse)response);
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
