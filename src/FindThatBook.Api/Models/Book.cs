namespace FindThatBook.Api.Models;

public sealed record Book(
    string Title,
    string Author,
    int PublishedYear,
    string Description);
