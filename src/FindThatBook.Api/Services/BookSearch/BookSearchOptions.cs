namespace FindThatBook.Api.Services.BookSearch;

public sealed class BookSearchOptions
{
    public const string SectionName = "BookSearch";

    public int CacheDurationMinutes { get; init; } = 60;
}
