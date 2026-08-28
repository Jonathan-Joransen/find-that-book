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
    string Explanation);
