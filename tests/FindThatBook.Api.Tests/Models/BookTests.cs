using System.Text.Json;
using FindThatBook.Api.Models;
using Xunit;

namespace FindThatBook.Api.Tests.Models;

public sealed class BookTests
{
    [Fact]
    public void JsonResponse_IncludesDescription()
    {
        var book = new Book(
            "Moby Dick",
            "Herman Melville",
            1851,
            "A sea captain pursues the white whale that maimed him.",
            "/works/OL102749W",
            "https://openlibrary.org/works/OL102749W",
            10521270,
            "https://covers.openlibrary.org/b/id/10521270-M.jpg",
            "It matches the reader's clues.",
            95);

        var json = JsonSerializer.Serialize(book, JsonSerializerOptions.Web);
        using var document = JsonDocument.Parse(json);

        Assert.Equal(
            "A sea captain pursues the white whale that maimed him.",
            document.RootElement.GetProperty("description").GetString());
    }
}
