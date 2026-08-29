using System.ComponentModel;
using FindThatBook.Api.Models;

namespace FindThatBook.Api.Models.LanguageModels;

public sealed record BookFinderCompletion(
    [property: Description("The strongest Open Library candidates, ordered from highest to lowest score.")]
    List<BookCandidateRanking> RankedBooks);

public sealed record BookCandidateRanking(
    [property: Description("The opaque candidate ID returned by the search_open_library tool.")]
    string CandidateId,
    [property: Description("How closely the candidate matches the reader's request, from 0 through 100.")]
    int Score,
    [property: Description("A concise explanation, under 200 characters, of why the candidate received this score.")]
    string Reason);

public sealed record RankedBook(
    int Score,
    string Reason,
    Book Book);
