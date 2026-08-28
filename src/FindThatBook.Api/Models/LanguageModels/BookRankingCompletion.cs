using System.ComponentModel;
using FindThatBook.Api.Models;

namespace FindThatBook.Api.Models.LanguageModels;

public sealed record BookRankingCompletion(
    [property: Description("Every candidate book paired with its search ranking score.")]
    List<RankedBook> RankedBooks);

public sealed record RankedBook(
    [property: Description("How closely the book matches the reader's request, from 0 through 100.")]
    int Score,
    [property: Description("A concise explanation, under 200 characters, of why the book received this score.")]
    string Reason,
    [property: Description("The complete candidate book with all original metadata unchanged.")]
    Book Book);
