namespace FindThatBook.Api.Providers.BookProviders.OpenLibrary;

public sealed class OpenLibraryOptions
{
    public const string SectionName = "OpenLibrary";

    public string BaseUrl { get; init; } = "https://openlibrary.org/";

    public int SearchLimit { get; init; } = 25;

    public int RetryCount { get; init; } = 2;

    public int RetryDelayMilliseconds { get; init; } = 250;

    public string UserAgent { get; init; } = "FindThatBook/1.0";
}
