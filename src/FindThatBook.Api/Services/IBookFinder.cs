using FindThatBook.Api.Models.LanguageModels;

namespace FindThatBook.Api.Services;

public interface IBookFinder
{
    Task<IReadOnlyList<RankedBook>> FindAsync(
        string query,
        CancellationToken cancellationToken = default);
}
