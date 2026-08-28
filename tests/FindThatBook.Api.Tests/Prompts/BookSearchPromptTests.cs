using FindThatBook.Api.Models.LanguageModels;
using FindThatBook.Api.Prompts;
using Xunit;

namespace FindThatBook.Api.Tests.Prompts;

public sealed class BookSearchPromptTests
{
    [Fact]
    public void Constructor_TrimsUserQueryAndVersionsPrompt()
    {
        var prompt = new BookSearchPrompt("  a whale and an obsessive captain  ");

        Assert.Equal("a whale and an obsessive captain", prompt.UserMessage);
        Assert.Equal(new PromptId("book-search", 1), prompt.Id);
    }

    [Fact]
    public void Validate_RejectsEmptyGeneratedQuery()
    {
        var prompt = new BookSearchPrompt("a sea story");

        Assert.Throws<InvalidDataException>(() =>
            prompt.Validate(new BookSearchCompletion("  ")));
    }

    [Fact]
    public void Validate_AcceptsUsefulGeneratedQuery()
    {
        var prompt = new BookSearchPrompt("a sea story");

        prompt.Validate(new BookSearchCompletion("Moby Dick Herman Melville sea adventure"));
    }
}
