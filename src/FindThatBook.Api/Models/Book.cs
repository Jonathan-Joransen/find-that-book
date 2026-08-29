namespace FindThatBook.Api.Models;

public sealed record Book(
    string Title,
    string Author,
    int? FirstPublishYear,
    string Description,
    string? BookKey,
    string? BookUrl,
    int? CoverId,
    string? CoverImageUrl,
    string Explanation,
    int? Score = null)
{
    public IReadOnlyList<BookAuthor> Authors { get; init; } = [];
}

public sealed record BookAuthor(
    string? AuthorKey,
    string Name,
    string? Role,
    bool IsPrimary,
    string Evidence);
