using System.Diagnostics;
using System.Text.Json;
using FindThatBook.Api.Models;
using FindThatBook.Api.Providers.BookProviders;
using Microsoft.Extensions.Options;

namespace FindThatBook.Api.Providers.BookProviders.OpenLibrary;

public sealed class OpenLibraryBookProvider : IBookProvider
{
    private const string Fields = "key,title,author_name,first_publish_year,description,cover_i";

    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenLibraryBookProvider> _logger;
    private readonly OpenLibraryOptions _options;

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
        return ReadBooks(responseBody);
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

    private IReadOnlyList<Book> ReadBooks(string responseBody)
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
            .Select(document => document.ToBook(_options))
            .ToArray();
    }

    private static string? JoinKeywords(IReadOnlyList<string>? keywords) =>
        keywords is { Count: > 0 } ? string.Join(' ', keywords) : null;
}
