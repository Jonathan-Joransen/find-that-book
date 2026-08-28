using System.ComponentModel;

namespace FindThatBook.Api.Models.LanguageModels;

public sealed record BookSearchCompletion(
    [property: Description("The likely book title, or null when no title can be identified.")]
    string? Title,
    [property: Description("The likely book author, or null when no author can be identified.")]
    string? Author,
    [property: Description("Distinct, independently useful subjects, settings, characters, objects, or concepts, or null when none can be identified.")]
    IReadOnlyList<string>? Keywords);
