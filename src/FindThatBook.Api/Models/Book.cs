namespace FindThatBook.Api.Models;

public sealed record Book(
    string Title,
    string Author,
    int? FirstPublishYear,
    string Description,
    string? OpenLibraryKey,
    string? OpenLibraryUrl,
    int? CoverId,
    string? CoverImageUrl,
    string Explanation);
