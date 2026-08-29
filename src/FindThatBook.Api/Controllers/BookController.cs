using FindThatBook.Api.Models;
using FindThatBook.Api.Models.Requests;
using FindThatBook.Api.Providers.LanguageModelProviders;
using FindThatBook.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FindThatBook.Api.Controllers;

[ApiController]
[Route("book")]
public sealed class BookController : ControllerBase
{
    private readonly IBookSearchService _bookSearchService;

    public BookController(IBookSearchService bookSearchService)
    {
        _bookSearchService = bookSearchService;
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
            var books = await _bookSearchService.SearchAsync(query, cancellationToken);
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
}
