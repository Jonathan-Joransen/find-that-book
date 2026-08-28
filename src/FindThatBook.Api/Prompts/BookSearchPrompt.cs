using FindThatBook.Api.Models.LanguageModels;

namespace FindThatBook.Api.Prompts;

public sealed class BookSearchPrompt : ILanguageModelPrompt<BookSearchCompletion>
{
    public BookSearchPrompt(string query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        UserMessage = query.Trim();
    }

    public PromptId Id => new("book-search", 1);

    public string SystemMessage =>
        """
        Convert the reader's description into a concise query for the Open Library search API.
        Include likely book titles, authors, subjects, settings, or distinctive plot keywords.
        Do not answer the reader and do not invent publication details.
        """;

    public string UserMessage { get; }

    public LanguageModelSettings Settings => new(
        Temperature: 0.2f,
        MaximumOutputTokens: 200);

    public void Validate(BookSearchCompletion response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (string.IsNullOrWhiteSpace(response.SearchQuery))
        {
            throw new InvalidDataException("The language model returned an empty search query.");
        }

        if (response.SearchQuery.Length > 500)
        {
            throw new InvalidDataException("The language model search query is too long.");
        }
    }
}
