using FindThatBook.Api.Extensions;
using FindThatBook.Api.Models;
using FindThatBook.Api.Providers.BookProviders;
using FindThatBook.Api.Providers.BookProviders.OpenLibrary;
using FindThatBook.Api.Providers.LanguageModelProviders.Gemini;
using FindThatBook.Api.Services.BookFinding;
using FindThatBook.Api.Services.BookSearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace FindThatBook.Api.Tests.Integration;

[Trait("Category", "LiveExternal")]
public sealed class LiveBookSearchIntegrationTests(ITestOutputHelper output)
{
    [LiveExternalTheory]
    [InlineData("a whale and an obsessive captain", "Moby", "Melville")]
    [InlineData("a scientist creates life from dead bodies", "Frankenstein", "Shelley")]
    [InlineData("Bilbo joins thirteen dwarves and meets the dragon Smaug", "Hobbit", "Tolkien")]
    public async Task SearchAsync_FindsExpectedBookThroughLiveOpenLibrary(
        string input,
        string expectedTitleFragment,
        string expectedAuthorFragment)
    {
        await using var serviceProvider = CreateOpenLibraryServiceProvider();
        var bookProvider = serviceProvider.GetRequiredService<IBookProvider>();
        using var languageModel = new GeminiLanguageModelProvider(
            Options.Create(GeminiIntegrationTestConfiguration.GetOptions()),
            NullLogger<GeminiLanguageModelProvider>.Instance);
        var finder = new LanguageModelBookFinder(
            bookProvider,
            languageModel,
            NullLogger<LanguageModelBookFinder>.Instance);
        var search = new BookSearchService(
            finder,
            NullLogger<BookSearchService>.Instance);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(120));

        var results = await search.SearchAsync(input, timeout.Token);

        output.WriteLine($"Input: {input}");
        foreach (var book in results)
        {
            output.WriteLine(
                $"{book.Score}: {book.Title} by {book.Author} ({book.BookKey}) - {book.Explanation}");
        }

        Assert.InRange(results.Count, 1, 12);
        Assert.All(results, book =>
        {
            Assert.NotNull(book.Score);
            Assert.InRange(book.Score!.Value, 61, 100);
            Assert.False(string.IsNullOrWhiteSpace(book.Explanation));
            Assert.True(book.Explanation.Length < 200);
        });
        Assert.True(
            results.Zip(results.Skip(1), (first, second) => first.Score >= second.Score).All(value => value),
            "Expected results to be ordered from highest to lowest score.");

        var expectedBook = results.FirstOrDefault(book =>
            book.Title.Contains(expectedTitleFragment, StringComparison.OrdinalIgnoreCase) &&
            book.Author.Contains(expectedAuthorFragment, StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(expectedBook);
    }

    private static ServiceProvider CreateOpenLibraryServiceProvider()
    {
        var defaults = new OpenLibraryOptions();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{OpenLibraryOptions.SectionName}:BaseUrl"] = defaults.BaseUrl,
                [$"{OpenLibraryOptions.SectionName}:SearchLimit"] = defaults.SearchLimit.ToString(),
                [$"{OpenLibraryOptions.SectionName}:RetryCount"] = defaults.RetryCount.ToString(),
                [$"{OpenLibraryOptions.SectionName}:RetryDelayMilliseconds"] = defaults.RetryDelayMilliseconds.ToString(),
                [$"{OpenLibraryOptions.SectionName}:UserAgent"] = "FindThatBook.Tests/1.0"
            })
            .Build();
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddOpenLibrary(configuration);

        return services.BuildServiceProvider();
    }
}
