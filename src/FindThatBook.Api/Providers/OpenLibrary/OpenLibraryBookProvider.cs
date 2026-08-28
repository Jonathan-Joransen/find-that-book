using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FindThatBook.Api.Models;
using Microsoft.Extensions.Options;

namespace FindThatBook.Api.Providers.OpenLibrary;

public sealed class OpenLibraryBookProvider : IBookProvider
{
    private const string Fields = "key,title,author_name,first_publish_year,first_sentence";
    private const string UnknownAuthor = "Unknown author";
    private const string MissingDescription = "No description is available from Open Library.";

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
        string bookInformation,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookInformation);

        var requestUri = $"search.json?q={Uri.EscapeDataString(bookInformation.Trim())}" +
                         $"&fields={Uri.EscapeDataString(Fields)}" +
                         $"&limit={_options.SearchLimit}";

        using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();

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
            .Select(MapBook)
            .ToArray();
    }

    private static Book MapBook(OpenLibrarySearchDocument document)
    {
        var authors = document.AuthorNames?
            .Where(author => !string.IsNullOrWhiteSpace(author))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var firstSentence = document.FirstSentences?
            .FirstOrDefault(sentence => !string.IsNullOrWhiteSpace(sentence));

        return new Book(
            document.Title!.Trim(),
            authors is { Length: > 0 } ? string.Join(", ", authors) : UnknownAuthor,
            document.FirstPublishYear ?? 0,
            firstSentence?.Trim() ?? MissingDescription);
    }

    private sealed class OpenLibrarySearchResponse
    {
        [JsonPropertyName("docs")]
        public List<OpenLibrarySearchDocument>? Documents { get; init; }
    }

    private sealed class OpenLibrarySearchDocument
    {
        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("author_name")]
        public List<string>? AuthorNames { get; init; }

        [JsonPropertyName("first_publish_year")]
        public int? FirstPublishYear { get; init; }

        [JsonPropertyName("first_sentence")]
        public List<string>? FirstSentences { get; init; }
    }
}
