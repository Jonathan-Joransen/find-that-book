using FindThatBook.Api.Models.LanguageModels;
using FindThatBook.Api.Prompts;
using Microsoft.Extensions.AI;
using Xunit;

namespace FindThatBook.Api.Tests.Prompts;

public sealed class BookSearchPromptTests
{
    [Fact]
    public void Constructor_TrimsUserQueryAndVersionsPrompt()
    {
        var prompt = new BookSearchPrompt("  a whale and an obsessive captain  ");

        Assert.Equal("a whale and an obsessive captain", prompt.UserMessage);
        Assert.Equal(new PromptId("book-search", 3), prompt.Id);
        Assert.Equal(1_024, prompt.Settings.MaximumOutputTokens);
        Assert.Equal(ReasoningEffort.Low, prompt.Settings.ReasoningEffort);
    }

    [Fact]
    public void Validate_RejectsResponseWithoutSearchEvidence()
    {
        var prompt = new BookSearchPrompt("a sea story");

        Assert.Throws<InvalidDataException>(() =>
            prompt.Validate(new BookSearchCompletion(null, "  ", null)));
    }

    [Fact]
    public void Validate_AcceptsPartialSearchEvidenceWithNullValues()
    {
        var prompt = new BookSearchPrompt("a sea story");

        prompt.Validate(new BookSearchCompletion(null, null, "sea adventure"));
    }
}
