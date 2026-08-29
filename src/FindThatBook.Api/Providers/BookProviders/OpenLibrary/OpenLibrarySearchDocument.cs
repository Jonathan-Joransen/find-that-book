using System.Text.Json.Serialization;
using FindThatBook.Api.Models;

namespace FindThatBook.Api.Providers.BookProviders.OpenLibrary;

internal sealed class OpenLibrarySearchDocument
{
    private const string UnknownAuthor = "Unknown author";
    private const string CoverBaseUrl = "https://covers.openlibrary.org/b/id/";

    [JsonPropertyName("key")]
    public string? Key { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("author_name")]
    public List<string>? AuthorNames { get; init; }

    [JsonPropertyName("first_publish_year")]
    public int? FirstPublishYear { get; init; }

    [JsonPropertyName("first_sentence")]
    public List<string>? FirstSentences { get; init; }

    [JsonPropertyName("cover_i")]
    public int? CoverId { get; init; }

    public Book ToBook(OpenLibraryOptions options)
    {
        var authors = AuthorNames?
            .Where(author => !string.IsNullOrWhiteSpace(author))
            .Select(author => author.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var firstSentence = FirstSentences?
            .FirstOrDefault(sentence => !string.IsNullOrWhiteSpace(sentence));
        var bookKey = NormalizeOpenLibraryKey(Key);
        var bookUrl = bookKey is null
            ? null
            : new Uri(new Uri(options.BaseUrl), bookKey.TrimStart('/')).ToString();
        var coverImageUrl = CoverId is null
            ? null
            : $"{CoverBaseUrl}{CoverId}-M.jpg";

        return new Book(
            Title!.Trim(),
            authors is { Length: > 0 } ? string.Join(", ", authors) : UnknownAuthor,
            FirstPublishYear,
            firstSentence?.Trim() ?? string.Empty,
            bookKey,
            bookUrl,
            CoverId,
            coverImageUrl,
            string.Empty);
    }

    private static string? NormalizeOpenLibraryKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var normalizedKey = key.Trim();
        if (normalizedKey.StartsWith('/'))
        {
            return normalizedKey;
        }

        return normalizedKey.StartsWith("OL", StringComparison.OrdinalIgnoreCase) &&
               normalizedKey.EndsWith('W')
            ? $"/works/{normalizedKey}"
            : $"/{normalizedKey}";
    }
}
