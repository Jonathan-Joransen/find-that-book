using FindThatBook.Api.Controllers;
using FindThatBook.Api.Models;
using FindThatBook.Api.Models.Requests;
using FindThatBook.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
        var controller = new BookController(service);

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
        var controller = new BookController(service);
        var query = new string('a', queryLength);

        var response = await controller.Search(
            new SearchBooksRequest($"  {query}  "),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response.Result);
        Assert.IsAssignableFrom<IReadOnlyList<Book>>(ok.Value);
        Assert.Equal(1, service.CallCount);
        Assert.Equal(query, service.Query);
    }

    private sealed class RecordingBookSearchService : IBookSearchService
    {
        public int CallCount { get; private set; }

        public string? Query { get; private set; }

        public Task<IReadOnlyList<Book>> SearchAsync(
            string query,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Query = query;
            return Task.FromResult<IReadOnlyList<Book>>([]);
        }
    }
}
