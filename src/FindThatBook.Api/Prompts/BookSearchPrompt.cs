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

    public PromptId Id => new("book-search", 5);

    public string SystemMessage =>
        """
        Interpret the reader's text as evidence for finding a book in the Open Library search API.
        Return the likely title, likely author, and concise distinctive keywords as separate fields.
        Use null for each field that cannot be identified; do not combine the fields into one query.
        Return keywords as a JSON array of lowercase strings, not as one space-delimited string.
        Each keyword must be independently useful for identifying the book in library metadata.
        Prefer concrete nouns, proper names, settings, characters, objects, and distinctive concepts.
        Exclude articles, conjunctions, search-language words, and generic relationship or action verbs such as "is", "has", "does", "makes", and "gets".
        Retain a verb only when the action itself is unusual and independently useful search evidence.
        Return no more than six keywords. Prefer a single word per item, but keep an established multi-word concept together.
        Normalize a title to the concise primary English title most commonly used by readers and libraries, with standard capitalization and punctuation.
        Do not expand a title with a subtitle, alternate title, or historical title variant unless the reader supplied that wording to distinguish the work.
        Do not add or remove a leading article such as "the" when normalizing a title supplied by the reader.
        Normalize an author to the full name most commonly printed on their books, with standard capitalization.
        Identify which words in the reader's text are title or author evidence before normalizing those fields.
        Consider the remaining explicit descriptive evidence for keywords, even when a keyword also occurs in a subtitle, alternate title, or expanded title that the reader did not supply as title evidence.
        Include only the remaining evidence that satisfies the keyword quality rules above.
        Normalize keywords to distinct lowercase strings, remove only words actually consumed as title or author evidence, and order the array alphabetically.
        Treat a short phrase that plausibly names a book as a title, even when it could also describe a subject.
        Reserve keywords for descriptive evidence rather than words already assigned to a likely title or author.
        For example, "moby whale" means title "Moby-Dick", author "Herman Melville", and keywords ["whale"]; do not expand the title to "Moby-Dick; or, The Whale" or remove the keyword.
        For example, "the book about a doctor who makes a monstor" means title "Frankenstein", author "Mary Shelley", and keywords ["doctor", "monster"]; "makes" is only a generic relationship verb.
        Use null rather than an empty string, placeholder, or unknown value.
        Do not answer the reader and do not invent publication details.
        """;

    public string UserMessage { get; }

    public LanguageModelSettings Settings => new(
        Temperature: 0.2f,
        MaximumOutputTokens: 1_024,
        ReasoningEffort: ReasoningEffort.Low);

    public BookSearchCompletion Normalize(BookSearchCompletion response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var keywords = response.Keywords?
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Select(keyword => keyword.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(keyword => keyword, StringComparer.Ordinal)
            .ToArray();

        return response with
        {
            Keywords = keywords is { Length: > 0 } ? keywords : null
        };
    }

    public void Validate(BookSearchCompletion response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (string.IsNullOrWhiteSpace(response.Title) &&
            string.IsNullOrWhiteSpace(response.Author) &&
            response.Keywords?.Any(keyword => !string.IsNullOrWhiteSpace(keyword)) != true)
        {
            throw new InvalidDataException("The language model returned no book search evidence.");
        }

        if (IsTooLong(response.Title) ||
            IsTooLong(response.Author) ||
            response.Keywords is { Count: > 6 } ||
            response.Keywords?.Any(IsTooLong) == true)
        {
            throw new InvalidDataException("The language model book search evidence is too long.");
        }
    }

    private static bool IsTooLong(string? value) => value?.Length > 500;
}
