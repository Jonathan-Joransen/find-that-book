using System.Security.Cryptography;
using System.Text;
using FindThatBook.Api.Models;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace FindThatBook.Api.Providers.BookProviders.OpenLibrary;

public sealed class CachedOpenLibraryBookProvider(
    OpenLibraryBookProvider innerProvider,
    HybridCache cache,
    IOptions<OpenLibraryOptions> options) : IBookProvider
{
    private readonly HybridCacheEntryOptions _cacheEntryOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(options.Value.SearchCacheDurationMinutes),
        LocalCacheExpiration = TimeSpan.FromMinutes(options.Value.SearchCacheDurationMinutes)
    };

    public async Task<IReadOnlyList<Book>> SearchAsync(
        BookSearchQuery search,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(search);

        return await cache.GetOrCreateAsync(
            CreateCacheKey(search, options.Value.SearchLimit),
            async token => (await innerProvider.SearchAsync(search, token)).ToArray(),
            _cacheEntryOptions,
            cancellationToken: cancellationToken);
    }

    private static string CreateCacheKey(BookSearchQuery search, int searchLimit)
    {
        var title = Normalize(search.Title);
        var author = Normalize(search.Author);
        var keywords = title.Length == 0 && author.Length == 0
            ? search.Keywords?.Select(Normalize).ToArray() ?? []
            : [];
        var source = new StringBuilder().Append(searchLimit);
        AppendCacheKeyPart(source, title);
        AppendCacheKeyPart(source, author);
        source.Append(':').Append(keywords.Length);

        foreach (var keyword in keywords)
        {
            AppendCacheKeyPart(source, keyword);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source.ToString()));

        return $"open-library:search:v1:{Convert.ToHexString(hash)}";
    }

    private static void AppendCacheKeyPart(StringBuilder builder, string value) =>
        builder.Append(':').Append(value.Length).Append(':').Append(value);

    private static string Normalize(string? value) =>
        value?.Trim().ToUpperInvariant() ?? string.Empty;
}
