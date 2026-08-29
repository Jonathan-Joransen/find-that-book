using System.Text.Json.Serialization;

namespace FindThatBook.Api.Providers.BookProviders.OpenLibrary;

internal sealed class OpenLibrarySearchResponse
{
    [JsonPropertyName("docs")]
    public List<OpenLibrarySearchDocument>? Documents { get; init; }
}
