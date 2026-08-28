using System.ComponentModel;

namespace FindThatBook.Api.Models.LanguageModels;

public sealed record BookSearchCompletion(
    [property: Description("The likely book title, or null when no title can be identified.")]
    string? Title,
    [property: Description("The likely book author, or null when no author can be identified.")]
    string? Author,
    [property: Description("Concise subjects, settings, plot details, or other distinctive search keywords, or null when none can be identified.")]
    string? Keywords);
