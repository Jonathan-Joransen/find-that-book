using FindThatBook.Api.Controllers;
using FindThatBook.Api.Models;
using FindThatBook.Api.Models.Requests;
using FindThatBook.Api.Services;
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
            string.Empty,
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

        Assert.IsType<OkObjectResult>(first.Result);
        Assert.IsType<OkObjectResult>(second.Result);
        Assert.Equal(1, service.CallCount);
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
}
