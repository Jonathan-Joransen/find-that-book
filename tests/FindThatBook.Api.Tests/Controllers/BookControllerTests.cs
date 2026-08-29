using FindThatBook.Api.Controllers;
using FindThatBook.Api.Models;
using FindThatBook.Api.Models.Requests;
using FindThatBook.Api.Providers.LanguageModelProviders;
using FindThatBook.Api.Services.BookSearch;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace FindThatBook.Api.Tests.Controllers;

public sealed class BookControllerTests
{
    public static TheoryData<string?> InvalidLengthQueries =>
        new()
        {
            null,
            "   ",
            new string('a', SearchBooksRequest.MaximumQueryLength + 1)
        };

    [Theory]
    [MemberData(nameof(InvalidLengthQueries))]
    public async Task Search_ReturnsSameBadRequestForQueryOutsideLengthRange(string? query)
    {
        var service = new RecordingBookSearchService();
        await using var services = CreateController(service);
        var controller = services.GetRequiredService<BookController>();

        var response = await controller.Search(
            new SearchBooksRequest(query),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        Assert.Null(problem.Status);
        Assert.Equal("The search query length is invalid.", problem.Title);
        Assert.Equal(
            $"Provide a query between {SearchBooksRequest.MinimumQueryLength} and {SearchBooksRequest.MaximumQueryLength} characters.",
            problem.Detail);
        Assert.Equal(0, service.CallCount);
    }

    [Theory]
    [InlineData(SearchBooksRequest.MinimumQueryLength)]
    [InlineData(SearchBooksRequest.MaximumQueryLength)]
    public async Task Search_AcceptsBoundaryLengthAndPassesTrimmedQueryToService(int queryLength)
    {
        var service = new RecordingBookSearchService();
        await using var services = CreateController(service);
        var controller = services.GetRequiredService<BookController>();
        var query = new string('a', queryLength);

        var response = await controller.Search(
            new SearchBooksRequest($"  {query}  "),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        Assert.IsAssignableFrom<IReadOnlyList<Book>>(ok.Value);
        Assert.Equal(1, service.CallCount);
        Assert.Equal(query, service.Query);
    }

    [Fact]
    public async Task Search_CachesRepeatedEquivalentRequests()
    {
        var service = new RecordingBookSearchService();
        await using var services = CreateController(service);
        var controller = services.GetRequiredService<BookController>();

        var first = await controller.Search(
            new SearchBooksRequest("  a whale and an obsessive captain  "),
            CancellationToken.None);
        var second = await controller.Search(
            new SearchBooksRequest("a whale and an obsessive captain"),
            CancellationToken.None);
        var distinct = await controller.Search(
            new SearchBooksRequest("a doctor creates a monster"),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(first.Result);
        Assert.IsType<OkObjectResult>(second.Result);
        Assert.IsType<OkObjectResult>(distinct.Result);
        Assert.Equal(2, service.CallCount);
    }

    [Fact]
    public async Task Search_CoalescesConcurrentEquivalentRequests()
    {
        var service = new RecordingBookSearchService(delayMilliseconds: 50);
        await using var services = CreateController(service);
        var controller = services.GetRequiredService<BookController>();

        var searches = Enumerable.Range(0, 5)
            .Select(_ => controller.Search(
                new SearchBooksRequest("a whale and an obsessive captain"),
                CancellationToken.None))
            .ToArray();

        await Task.WhenAll(searches);

        Assert.Equal(1, service.CallCount);
    }

    [Theory]
    [InlineData(
        true,
        "The language model provider is unavailable.",
        "The book search could not be completed. Try again later.")]
    [InlineData(
        false,
        "The book provider is unavailable.",
        "Open Library could not complete the search. Try again later.")]
    public async Task Search_ReturnsBadGatewayWhenDependencyIsUnavailable(
        bool languageModelFailure,
        string expectedTitle,
        string expectedDetail)
    {
        await using var services = CreateController(
            new ThrowingBookSearchService(languageModelFailure));
        var controller = services.GetRequiredService<BookController>();

        var response = await controller.Search(
            new SearchBooksRequest("a whale and an obsessive captain"),
            CancellationToken.None);

        var error = Assert.IsType<ObjectResult>(response.Result);
        var problem = Assert.IsType<ProblemDetails>(error.Value);
        Assert.Equal(StatusCodes.Status502BadGateway, error.StatusCode);
        Assert.Equal(StatusCodes.Status502BadGateway, problem.Status);
        Assert.Equal(expectedTitle, problem.Title);
        Assert.Equal(expectedDetail, problem.Detail);
    }

    private static ServiceProvider CreateController(IBookSearchService service)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddHybridCache();
        services.AddSingleton(service);
        services.AddSingleton(Options.Create(new BookSearchOptions()));
        services.AddTransient<BookController>();

        return services.BuildServiceProvider();
    }

    private sealed class RecordingBookSearchService(int delayMilliseconds = 0) : IBookSearchService
    {
        private int _callCount;

        public int CallCount => _callCount;

        public string? Query { get; private set; }

        public async Task<IReadOnlyList<Book>> SearchAsync(
            string query,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            Query = query;

            if (delayMilliseconds > 0)
            {
                await Task.Delay(delayMilliseconds, cancellationToken);
            }

            return [];
        }
    }

    private sealed class ThrowingBookSearchService(bool languageModelFailure) : IBookSearchService
    {
        public Task<IReadOnlyList<Book>> SearchAsync(
            string query,
            CancellationToken cancellationToken = default) =>
            languageModelFailure
                ? throw new LanguageModelException(
                    "Language model unavailable.",
                    new HttpRequestException("Provider request failed."))
                : throw new HttpRequestException("Open Library unavailable.");
    }
}
