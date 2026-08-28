using System.ComponentModel;

namespace FindThatBook.Api.Models.LanguageModels;

public sealed record BookSearchCompletion(
    [property: Description("A concise Open Library search query containing likely titles, authors, subjects, or keywords.")]
    string SearchQuery);
