using FindThatBook.Api.Models;

namespace FindThatBook.Api.Providers.BookProviders;

public interface IBookProvider
{
    Task<IReadOnlyList<Book>> SearchAsync(
        BookSearchQuery search,
        CancellationToken cancellationToken = default);
}
