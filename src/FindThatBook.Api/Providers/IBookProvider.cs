using FindThatBook.Api.Models;

namespace FindThatBook.Api.Providers;

public interface IBookProvider
{
    Task<IReadOnlyList<Book>> SearchAsync(
        string bookInformation,
        CancellationToken cancellationToken = default);
}
