using System.Security.Cryptography;
using System.Text;
using FindThatBook.Api.Models;
using FindThatBook.Api.Models.Requests;
using FindThatBook.Api.Providers.LanguageModelProviders;
using FindThatBook.Api.Services.BookSearch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace FindThatBook.Api.Controllers;

[ApiController]
[Route("book")]
public sealed class BookController : ControllerBase
{
    private readonly IBookSearchService _bookSearchService;
    private readonly HybridCache _cache;
    private readonly HybridCacheEntryOptions _cacheEntryOptions;

    public BookController(
        IBookSearchService bookSearchService,
        HybridCache cache,
        IOptions<BookSearchOptions> options)
    {
        _bookSearchService = bookSearchService;
        _cache = cache;
        _cacheEntryOptions = new HybridCacheEntryOptions
        {
            Expiration = TimeSpan.FromMinutes(options.Value.CacheDurationMinutes),
            LocalCacheExpiration = TimeSpan.FromMinutes(options.Value.CacheDurationMinutes)
        };
    }

    [HttpPost("search")]
    [ProducesResponseType<IReadOnlyList<Book>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<IReadOnlyList<Book>>> Search(
        [FromBody] SearchBooksRequest request,
        CancellationToken cancellationToken)
    {
        var query = request.Query?.Trim();

        if (query is null ||
            query.Length is < SearchBooksRequest.MinimumQueryLength or > SearchBooksRequest.MaximumQueryLength)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "The search query length is invalid.",
                Detail = $"Provide a query between {SearchBooksRequest.MinimumQueryLength} and {SearchBooksRequest.MaximumQueryLength} characters."
            });
        }

        try
        {
            var books = await _cache.GetOrCreateAsync(
                CreateCacheKey(query),
                async token => (await _bookSearchService.SearchAsync(query, token)).ToArray(),
                _cacheEntryOptions,
                cancellationToken: cancellationToken);
            return Ok(books);
        }
        catch (LanguageModelException)
        {
            return Problem(
                title: "The language model provider is unavailable.",
                detail: "The book search could not be completed. Try again later.",
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (HttpRequestException)
        {
            return Problem(
                title: "The book provider is unavailable.",
                detail: "Open Library could not complete the search. Try again later.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static string CreateCacheKey(string query)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(query));
        return $"book-search:v1:{Convert.ToHexString(hash)}";
    }
}
