using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FindThatBook.Api.Models;
using FindThatBook.Api.Providers.BookProviders;
using Microsoft.Extensions.Options;

namespace FindThatBook.Api.Providers.BookProviders.OpenLibrary;

public sealed class OpenLibraryBookProvider : IBookProvider
{
    private const string Fields = "key,title,author_name,author_key,first_publish_year,first_sentence,cover_i";
    private const string UnknownAuthor = "Unknown author";
    private const string CoverBaseUrl = "https://covers.openlibrary.org/b/id/";
    private const string CanonicalWorkEvidence = "canonicalWork";
    private const string SearchResultEvidence = "searchResult";

    private static readonly HashSet<string> NonDistinctiveTerms = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "at", "by", "for", "from", "in", "of", "on", "the", "to", "with"
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenLibraryBookProvider> _logger;
    private readonly OpenLibraryOptions _options;
    private readonly ConcurrentDictionary<string, Task<OpenLibraryWorkDocument?>> _worksByKey =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Task<string?>> _authorNamesByKey =
        new(StringComparer.OrdinalIgnoreCase);

    public OpenLibraryBookProvider(
        HttpClient httpClient,
        IOptions<OpenLibraryOptions> options,
        ILogger<OpenLibraryBookProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Book>> SearchAsync(
        BookSearchQuery search,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(search);

        var searchParameters = new List<string>();
        AddSearchParameter(searchParameters, "title", search.Title);
        AddSearchParameter(searchParameters, "author", search.Author);

        if (string.IsNullOrWhiteSpace(search.Title) &&
            string.IsNullOrWhiteSpace(search.Author))
        {
            AddSearchParameter(searchParameters, "q", JoinKeywords(search.Keywords));
        }

        if (searchParameters.Count == 0)
        {
            throw new ArgumentException(
                "At least one title, author, or keyword value is required.",
                nameof(search));
        }

        searchParameters.Add($"fields={Uri.EscapeDataString(Fields)}");
        searchParameters.Add($"limit={_options.SearchLimit}");
        var requestUri = $"search.json?{string.Join('&', searchParameters)}";

        _logger.LogInformation("Sending Open Library request to {RequestUri}.", requestUri);

        var startedAt = Stopwatch.GetTimestamp();
        using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogInformation(
            "Open Library returned HTTP {StatusCode} in {ElapsedMilliseconds} ms: {Response}",
            (int)response.StatusCode,
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
            responseBody);

        response.EnsureSuccessStatusCode();
        var documents = ReadSearchDocuments(responseBody);
        var books = await Task.WhenAll(documents.Select(
            (document, index) => MapBookAsync(document, search, index, cancellationToken)));

        return books;
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

    private static IReadOnlyList<OpenLibrarySearchDocument> ReadSearchDocuments(string responseBody)
    {
        OpenLibrarySearchResponse? searchResponse;

        try
        {
            searchResponse = JsonSerializer.Deserialize<OpenLibrarySearchResponse>(responseBody);
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
            .ToArray();
    }

    private async Task<Book> MapBookAsync(
        OpenLibrarySearchDocument document,
        BookSearchQuery search,
        int resultIndex,
        CancellationToken cancellationToken)
    {
        var searchAuthors = BuildSearchAuthors(document);
        var work = resultIndex < _options.WorkEnrichmentLimit
            ? await TryGetWorkAsync(document.Key, cancellationToken)
            : null;
        var authors = work?.Authors is { Count: > 0 }
            ? await ResolveWorkAuthorsAsync(work.Authors, searchAuthors, cancellationToken)
            : searchAuthors;
        var canonicalPrimaryAuthors = authors
            .Where(author => author.IsPrimary && author.Evidence == CanonicalWorkEvidence)
            .Select(author => author.Name)
            .Where(name => !string.Equals(name, UnknownAuthor, StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var hasCanonicalAuthorData = authors.Any(
            author => author.Evidence == CanonicalWorkEvidence);
        var fallbackAuthorNames = searchAuthors
            .Select(author => author.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var displayAuthor = canonicalPrimaryAuthors.Length > 0
            ? string.Join(", ", canonicalPrimaryAuthors)
            : hasCanonicalAuthorData
                ? UnknownAuthor
                : fallbackAuthorNames.Length > 0
                    ? string.Join(", ", fallbackAuthorNames)
                    : UnknownAuthor;

        var firstSentence = document.FirstSentences?
            .FirstOrDefault(sentence => !string.IsNullOrWhiteSpace(sentence));
        var workDescription = ReadText(work?.Description);

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
            displayAuthor,
            document.FirstPublishYear,
            workDescription ?? firstSentence?.Trim() ?? string.Empty,
            bookKey,
            bookUrl,
            document.CoverId,
            coverImageUrl,
            BuildMatchExplanation(search, title, authors, workDescription ?? firstSentence))
        {
            Authors = authors
        };
    }

    private static IReadOnlyList<BookAuthor> BuildSearchAuthors(OpenLibrarySearchDocument document)
    {
        if (document.AuthorNames is not { Count: > 0 })
        {
            return [];
        }

        return document.AuthorNames
            .Select((name, index) => new BookAuthor(
                NormalizeAuthorKey(document.AuthorKeys?.ElementAtOrDefault(index)),
                name?.Trim() ?? string.Empty,
                null,
                false,
                SearchResultEvidence))
            .Where(author => !string.IsNullOrWhiteSpace(author.Name))
            .DistinctBy(
                author => author.AuthorKey ?? author.Name,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<IReadOnlyList<BookAuthor>> ResolveWorkAuthorsAsync(
        IReadOnlyList<OpenLibraryWorkAuthor> workAuthors,
        IReadOnlyList<BookAuthor> searchAuthors,
        CancellationToken cancellationToken)
    {
        var searchNamesByKey = searchAuthors
            .Where(author => author.AuthorKey is not null)
            .ToDictionary(
                author => author.AuthorKey!,
                author => author.Name,
                StringComparer.OrdinalIgnoreCase);
        var resolvedAuthors = new List<BookAuthor>();

        foreach (var workAuthor in workAuthors)
        {
            var authorKey = NormalizeAuthorKey(workAuthor.Author?.Key);
            if (authorKey is null)
            {
                continue;
            }

            searchNamesByKey.TryGetValue(authorKey, out var name);
            name ??= await TryGetAuthorNameAsync(authorKey, cancellationToken);
            name ??= string.IsNullOrWhiteSpace(workAuthor.As)
                ? UnknownAuthor
                : workAuthor.As.Trim();
            var role = string.IsNullOrWhiteSpace(workAuthor.Role)
                ? null
                : workAuthor.Role.Trim();

            resolvedAuthors.Add(new BookAuthor(
                authorKey,
                name,
                role,
                IsPrimaryAuthorRole(role),
                CanonicalWorkEvidence));
        }

        return resolvedAuthors
            .DistinctBy(
                author => $"{author.AuthorKey}\u001f{author.Role}",
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<OpenLibraryWorkDocument?> TryGetWorkAsync(
        string? workKey,
        CancellationToken cancellationToken)
    {
        var normalizedKey = NormalizeOpenLibraryKey(workKey);
        if (normalizedKey is null || !normalizedKey.StartsWith("/works/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return await _worksByKey.GetOrAdd(
            normalizedKey,
            _ => FetchWorkAsync(normalizedKey, cancellationToken));
    }

    private async Task<OpenLibraryWorkDocument?> FetchWorkAsync(
        string normalizedKey,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                $"{normalizedKey.TrimStart('/')}.json",
                cancellationToken);
            response.EnsureSuccessStatusCode();
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<OpenLibraryWorkDocument>(responseBody);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            _logger.LogWarning(
                exception,
                "Could not enrich Open Library work {WorkKey}; using search metadata.",
                normalizedKey);
            return null;
        }
    }

    private async Task<string?> TryGetAuthorNameAsync(
        string authorKey,
        CancellationToken cancellationToken) =>
        await _authorNamesByKey.GetOrAdd(
            authorKey,
            _ => FetchAuthorNameAsync(authorKey, cancellationToken));

    private async Task<string?> FetchAuthorNameAsync(
        string authorKey,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(
                $"{authorKey.TrimStart('/')}.json",
                cancellationToken);
            response.EnsureSuccessStatusCode();
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<OpenLibraryAuthorDocument>(responseBody)?.Name?.Trim();
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            _logger.LogWarning(
                exception,
                "Could not resolve Open Library author {AuthorKey}.",
                authorKey);
            return null;
        }
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

    private static string? NormalizeAuthorKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var normalizedKey = key.Trim();
        if (normalizedKey.StartsWith("/authors/", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedKey;
        }

        return normalizedKey.StartsWith("OL", StringComparison.OrdinalIgnoreCase) &&
               normalizedKey.EndsWith('A')
            ? $"/authors/{normalizedKey}"
            : null;
    }

    private static bool IsPrimaryAuthorRole(string? role) =>
        string.IsNullOrWhiteSpace(role) ||
        role.Contains("author", StringComparison.OrdinalIgnoreCase) ||
        role.Contains("writer", StringComparison.OrdinalIgnoreCase);

    private static string? ReadText(JsonElement? element)
    {
        if (element is null || element.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (element.Value.ValueKind == JsonValueKind.String)
        {
            return NormalizeOptionalText(element.Value.GetString());
        }

        if (element.Value.ValueKind == JsonValueKind.Object &&
            element.Value.TryGetProperty("value", out var value) &&
            value.ValueKind == JsonValueKind.String)
        {
            return NormalizeOptionalText(value.GetString());
        }

        return null;
    }

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string BuildMatchExplanation(
        BookSearchQuery search,
        string title,
        IReadOnlyList<BookAuthor> authors,
        string? description)
    {
        var normalizedTitleEvidence = NormalizeForComparison(search.Title);
        var normalizedAuthorEvidence = NormalizeForComparison(search.Author);
        var exactTitleMatch = normalizedTitleEvidence.Contains(
            NormalizeForComparison(title),
            StringComparison.Ordinal);
        var exactPrimaryAuthorMatch = authors.Any(author =>
            author.IsPrimary &&
            normalizedAuthorEvidence.Contains(
                NormalizeForComparison(author.Name),
                StringComparison.Ordinal));
        var exactListedAuthorMatch = authors.Any(author =>
            normalizedAuthorEvidence.Contains(
                NormalizeForComparison(author.Name),
                StringComparison.Ordinal));

        if (exactTitleMatch && exactPrimaryAuthorMatch)
        {
            return "Strong title and canonical work-author match.";
        }

        if (exactTitleMatch && exactListedAuthorMatch)
        {
            return "Strong title match with supporting author metadata.";
        }

        if (exactTitleMatch)
        {
            return "Strong title match.";
        }

        var queryEvidence = new[] { search.Title, search.Author }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Concat(search.Keywords ?? []);
        var queryTerms = GetDistinctiveTerms(string.Join(' ', queryEvidence));
        var titleTermMatches = GetDistinctiveTerms(title).Count(queryTerms.Contains);
        var primaryAuthorTermMatch = authors
            .Where(author => author.IsPrimary)
            .SelectMany(author => GetDistinctiveTerms(author.Name))
            .Any(queryTerms.Contains);
        var listedAuthorTermMatch = authors
            .SelectMany(author => GetDistinctiveTerms(author.Name))
            .Any(queryTerms.Contains);

        if (titleTermMatches > 0 && primaryAuthorTermMatch)
        {
            return "The title and canonical work-author metadata both match the query.";
        }

        if (primaryAuthorTermMatch || exactPrimaryAuthorMatch)
        {
            return "A canonical work author matches the query.";
        }

        if (listedAuthorTermMatch || exactListedAuthorMatch)
        {
            return "Open Library's search author metadata matches the query.";
        }

        if (titleTermMatches > 0)
        {
            return "The title shares distinctive terms with the query.";
        }

        var descriptionTermMatches = string.IsNullOrWhiteSpace(description)
            ? 0
            : GetDistinctiveTerms(description).Count(queryTerms.Contains);

        return descriptionTermMatches > 0
            ? "The canonical work description shares details with the query."
            : "Open Library ranked this work as relevant to the query.";
    }

    private static string NormalizeForComparison(string? value) =>
        string.Concat((value ?? string.Empty).Where(char.IsLetterOrDigit)).ToLowerInvariant();

    private static string? JoinKeywords(IReadOnlyList<string>? keywords) =>
        keywords is { Count: > 0 } ? string.Join(' ', keywords) : null;

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

        [JsonPropertyName("author_key")]
        public List<string>? AuthorKeys { get; init; }

        [JsonPropertyName("first_publish_year")]
        public int? FirstPublishYear { get; init; }

        [JsonPropertyName("first_sentence")]
        public List<string>? FirstSentences { get; init; }

        [JsonPropertyName("cover_i")]
        public int? CoverId { get; init; }
    }

    private sealed class OpenLibraryWorkDocument
    {
        [JsonPropertyName("authors")]
        public List<OpenLibraryWorkAuthor>? Authors { get; init; }

        [JsonPropertyName("description")]
        public JsonElement Description { get; init; }
    }

    private sealed class OpenLibraryWorkAuthor
    {
        [JsonPropertyName("author")]
        public OpenLibraryKeyReference? Author { get; init; }

        [JsonPropertyName("role")]
        public string? Role { get; init; }

        [JsonPropertyName("as")]
        public string? As { get; init; }
    }

    private sealed class OpenLibraryKeyReference
    {
        [JsonPropertyName("key")]
        public string? Key { get; init; }
    }

    private sealed class OpenLibraryAuthorDocument
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }
}
