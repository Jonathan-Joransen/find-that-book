using FindThatBook.Api.Models;
using FindThatBook.Api.Models.LanguageModels;

namespace FindThatBook.Api.Providers;

public interface IBookProvider
{
    Task<IReadOnlyList<Book>> SearchAsync(
        BookSearchCompletion search,
        CancellationToken cancellationToken = default);
}
