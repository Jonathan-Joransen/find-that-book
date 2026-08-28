using FindThatBook.Api.Models.LanguageModels;
using Microsoft.Extensions.AI;

namespace FindThatBook.Api.Prompts;

public sealed class BookSearchPrompt : ILanguageModelPrompt<BookSearchCompletion>
{
    public BookSearchPrompt(string query)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        UserMessage = query.Trim();
    }

    public PromptId Id => new("book-search", 3);

    public string SystemMessage =>
        """
        Interpret the reader's text as evidence for finding a book in the Open Library search API.
        Return the likely title, likely author, and concise distinctive keywords as separate fields.
        Use null for each field that cannot be identified; do not combine the fields into one query.
        Keywords may include subjects, settings, characters, or plot details that would help identify the book.
        Normalize a title to its canonical English publication title, including standard capitalization and punctuation.
        Normalize an author to the full name most commonly printed on their books, with standard capitalization.
        Normalize keywords to distinct lowercase terms, remove title and author duplicates, and order multiple terms alphabetically.
        Treat a short phrase that plausibly names a book as a title, even when it could also describe a subject.
        Reserve keywords for descriptive evidence rather than words already assigned to a likely title or author.
        Use null rather than an empty string, placeholder, or unknown value.
        Do not answer the reader and do not invent publication details.
        """;

    public string UserMessage { get; }

    public LanguageModelSettings Settings => new(
        Temperature: 0.2f,
        MaximumOutputTokens: 1_024,
        ReasoningEffort: ReasoningEffort.Low);

    public void Validate(BookSearchCompletion response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (string.IsNullOrWhiteSpace(response.Title) &&
            string.IsNullOrWhiteSpace(response.Author) &&
            string.IsNullOrWhiteSpace(response.Keywords))
        {
            throw new InvalidDataException("The language model returned no book search evidence.");
        }

        if (IsTooLong(response.Title) ||
            IsTooLong(response.Author) ||
            IsTooLong(response.Keywords))
        {
            throw new InvalidDataException("The language model book search evidence is too long.");
        }
    }

    private static bool IsTooLong(string? value) => value?.Length > 500;
}
