using FindThatBook.Api.Models.LanguageModels;
using FindThatBook.Api.Prompts;
using FindThatBook.Api.Providers.Gemini;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace FindThatBook.Api.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class BookSearchIntegrationTests(ITestOutputHelper output)
{
    [GeminiIntegrationTheory]
    [InlineData("dickens", null, "Charles Dickens", null, false)]
    [InlineData("prince of wales", "Prince of Wales", null, null, false)]
    [InlineData("moby whale", "Moby-Dick", "Herman Melville", "whale", false)]
    [InlineData("the book about a doctor who makes a monstor", "Frankenstein", "Shelley", "doctor monster", true)]
    public async Task GenerateAsync_ReturnsExpectedBookSearchCompletion(
        string input,
        string? expectedTitle,
        string? expectedAuthor,
        string? expectedKeywords,
        bool expectedTitleAndAuthorAreFragments)
    {
        using var languageModel = CreateLanguageModel();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var response = await languageModel.GenerateAsync(
            new BookSearchPrompt(input),
            timeout.Token);

        output.WriteLine($"Input: {input}");
        output.WriteLine($"Title: {response.Title ?? "<null>"}");
        output.WriteLine($"Author: {response.Author ?? "<null>"}");
        output.WriteLine($"Keywords: {(response.Keywords is null ? "<null>" : string.Join(", ", response.Keywords))}");

        if (expectedTitleAndAuthorAreFragments)
        {
            Assert.Contains(expectedTitle!, response.Title, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(expectedAuthor!, response.Author, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.Equal(expectedTitle, response.Title);
            Assert.Equal(expectedAuthor, response.Author);
        }

        Assert.Equal(expectedKeywords?.Split(' '), response.Keywords);
    }

    private static GeminiLanguageModelProvider CreateLanguageModel()
        => new(
            Options.Create(GeminiIntegrationTestConfiguration.GetOptions()),
            NullLogger<GeminiLanguageModelProvider>.Instance);
}
