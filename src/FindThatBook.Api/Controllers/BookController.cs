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
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "A search query is required.",
                Detail = "Provide a non-empty query value in the request body.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        try
        {
            var books = await _bookSearchService.SearchAsync(request.Query, cancellationToken);
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
