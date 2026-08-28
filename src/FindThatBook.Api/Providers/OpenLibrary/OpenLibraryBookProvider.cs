using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FindThatBook.Api.Models;
using FindThatBook.Api.Models.LanguageModels;
using Microsoft.Extensions.Options;

namespace FindThatBook.Api.Providers.OpenLibrary;

public sealed class OpenLibraryBookProvider : IBookProvider
{
    private const string Fields = "key,title,author_name,first_publish_year,first_sentence,cover_i";
    private const string UnknownAuthor = "Unknown author";
    private const string CoverBaseUrl = "https://covers.openlibrary.org/b/id/";

    private static readonly HashSet<string> NonDistinctiveTerms = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "at", "by", "for", "from", "in", "of", "on", "the", "to", "with"
    };

    private readonly HttpClient _httpClient;
    private readonly OpenLibraryOptions _options;

    public OpenLibraryBookProvider(
        HttpClient httpClient,
        IOptions<OpenLibraryOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<Book>> SearchAsync(
        BookSearchCompletion search,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(search);

        var searchParameters = new List<string>();
        AddSearchParameter(searchParameters, "title", search.Title);
        AddSearchParameter(searchParameters, "author", search.Author);
        AddSearchParameter(searchParameters, "q", search.Keywords);

        if (searchParameters.Count == 0)
        {
            throw new ArgumentException(
                "At least one title, author, or keyword value is required.",
                nameof(search));
        }

        searchParameters.Add($"fields={Uri.EscapeDataString(Fields)}");
        searchParameters.Add($"limit={_options.SearchLimit}");
        var requestUri = $"search.json?{string.Join('&', searchParameters)}";

        using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadBooksAsync(response, search, cancellationToken);
    }

    private static void AddSearchParameter(
        ICollection<string> parameters,
        string name,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parameters.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
        }
    }

    private async Task<IReadOnlyList<Book>> ReadBooksAsync(
        HttpResponseMessage response,
        BookSearchCompletion search,
        CancellationToken cancellationToken)
    {
        OpenLibrarySearchResponse? searchResponse;

        try
        {
            searchResponse = await response.Content.ReadFromJsonAsync<OpenLibrarySearchResponse>(
                cancellationToken: cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new HttpRequestException(
                "Open Library returned a response that could not be read.",
                exception);
        }

        if (searchResponse?.Documents is not { Count: > 0 })
        {
            return [];
        }

        return searchResponse.Documents
            .Where(document => !string.IsNullOrWhiteSpace(document.Title))
            .Select(document => MapBook(document, search))
            .ToArray();
    }

    private Book MapBook(OpenLibrarySearchDocument document, BookSearchCompletion search)
    {
        var authors = document.AuthorNames?
            .Where(author => !string.IsNullOrWhiteSpace(author))
            .Select(author => author.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var firstSentence = document.FirstSentences?
            .FirstOrDefault(sentence => !string.IsNullOrWhiteSpace(sentence));

        var title = document.Title!.Trim();
        var bookKey = NormalizeOpenLibraryKey(document.Key);
        var bookUrl = bookKey is null
            ? null
            : new Uri(new Uri(_options.BaseUrl), bookKey.TrimStart('/')).ToString();
        var coverImageUrl = document.CoverId is null
            ? null
            : $"{CoverBaseUrl}{document.CoverId}-M.jpg";

        return new Book(
            title,
            authors is { Length: > 0 } ? string.Join(", ", authors) : UnknownAuthor,
            document.FirstPublishYear,
            firstSentence?.Trim() ?? string.Empty,
            bookKey,
            bookUrl,
            document.CoverId,
            coverImageUrl,
            BuildMatchExplanation(search, title, authors, firstSentence));
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

    private static string BuildMatchExplanation(
        BookSearchCompletion search,
        string title,
        IReadOnlyList<string>? authors,
        string? firstSentence)
    {
        var normalizedTitleEvidence = NormalizeForComparison(search.Title);
        var normalizedAuthorEvidence = NormalizeForComparison(search.Author);
        var exactTitleMatch = normalizedTitleEvidence.Contains(
            NormalizeForComparison(title),
            StringComparison.Ordinal);
        var exactAuthorMatch = authors?.Any(author => normalizedAuthorEvidence.Contains(
            NormalizeForComparison(author),
            StringComparison.Ordinal)) == true;

        if (exactTitleMatch && exactAuthorMatch)
        {
            return "Strong title and primary-author match.";
        }

        if (exactTitleMatch)
        {
            return "Strong title match.";
        }

        var queryTerms = GetDistinctiveTerms(
            string.Join(' ', new[] { search.Title, search.Author, search.Keywords }
                .Where(value => !string.IsNullOrWhiteSpace(value))));
        var titleTermMatches = GetDistinctiveTerms(title).Count(queryTerms.Contains);
        var authorTermMatch = authors?.SelectMany(GetDistinctiveTerms).Any(queryTerms.Contains) == true;

        if (titleTermMatches > 0 && authorTermMatch)
        {
            return "The title and primary-author metadata both match terms in the query.";
        }

        if (authorTermMatch || exactAuthorMatch)
        {
            return "The primary author matches the query.";
        }

        if (titleTermMatches > 0)
        {
            return "The title shares distinctive terms with the query.";
        }

        var openingTermMatches = string.IsNullOrWhiteSpace(firstSentence)
            ? 0
            : GetDistinctiveTerms(firstSentence).Count(queryTerms.Contains);

        return openingTermMatches > 0
            ? "The book's opening text shares details with the query."
            : "Open Library ranked this work as relevant to the query.";
    }

    private static string NormalizeForComparison(string? value) =>
        string.Concat((value ?? string.Empty).Where(char.IsLetterOrDigit)).ToLowerInvariant();

    private static HashSet<string> GetDistinctiveTerms(string value) =>
        Regex.Split(value.ToLowerInvariant(), @"[^\p{L}\p{N}]+")
            .Where(term => term.Length > 1 && !NonDistinctiveTerms.Contains(term))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private sealed class OpenLibrarySearchResponse
    {
        [JsonPropertyName("docs")]
        public List<OpenLibrarySearchDocument>? Documents { get; init; }
    }

    private sealed class OpenLibrarySearchDocument
    {
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
    }
}
