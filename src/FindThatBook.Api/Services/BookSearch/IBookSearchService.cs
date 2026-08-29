using FindThatBook.Api.Models;

namespace FindThatBook.Api.Services.BookSearch;

public interface IBookSearchService
{
    Task<IReadOnlyList<Book>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);
}
