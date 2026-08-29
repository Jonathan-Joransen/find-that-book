using FindThatBook.Api.Models;

namespace FindThatBook.Api.Providers;

public interface IBookProvider
{
    Task<IReadOnlyList<Book>> SearchAsync(
        BookSearchQuery search,
        CancellationToken cancellationToken = default);
}
