namespace FindThatBook.Api.Models;

public sealed record BookSearchQuery(
    string? Title,
    string? Author,
    IReadOnlyList<string>? Keywords);
