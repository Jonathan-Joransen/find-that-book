using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using FindThatBook.Api.Models;
using FindThatBook.Api.Models.LanguageModels;
using FindThatBook.Api.Providers.BookProviders;
using Microsoft.Extensions.AI;

namespace FindThatBook.Api.Services;

internal sealed class BookSearchSession
{
    internal const int MaximumSearches = 3;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };
    private static readonly MethodInfo SearchMethod = typeof(BookSearchSession)
        .GetMethod(nameof(SearchOpenLibraryAsync))!;

    private readonly Dictionary<string, Book> _booksByCandidateId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _candidateIdsByBook = new(StringComparer.Ordinal);
    private readonly IBookProvider _bookProvider;
    private readonly ILogger _logger;
    private int _nextCandidateId;

    public BookSearchSession(IBookProvider bookProvider, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(bookProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _bookProvider = bookProvider;
        _logger = logger;
        Tool = AIFunctionFactory.Create(
            SearchMethod,
            this,
            new AIFunctionFactoryOptions
            {
                Name = "search_open_library",
                Description =
                    "Search Open Library for candidate books using a likely title, author, or concise distinctive keywords.",
                SerializerOptions = JsonOptions
            });
    }

    public AIFunction Tool { get; }

    public int SearchCount { get; private set; }

    [Description("Search Open Library for books that may match the reader's description.")]
    public async Task<BookSearchToolResult> SearchOpenLibraryAsync(
        [Description("Likely book title, or null when unknown.")] string? title = null,
        [Description("Likely author name, or null when unknown.")] string? author = null,
        [Description("Up to six concise distinctive subjects, settings, characters, objects, or concepts, or null when unknown.")] string[]? keywords = null,
        CancellationToken cancellationToken = default)
    {
        if (SearchCount >= MaximumSearches)
        {
            throw new InvalidOperationException(
                $"A book-finding request may search Open Library at most {MaximumSearches} times.");
        }

        var normalizedTitle = NormalizeOptionalText(title);
        var normalizedAuthor = NormalizeOptionalText(author);
        var normalizedKeywords = keywords?
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Select(keyword => keyword.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Take(6)
            .ToArray();

        if (normalizedTitle is null &&
            normalizedAuthor is null &&
            normalizedKeywords is not { Length: > 0 })
        {
            throw new ArgumentException(
                "Provide at least one likely title, author, or distinctive keyword.");
        }

        SearchCount++;
        _logger.LogInformation(
            "Book finder tool search {SearchNumber} of {MaximumSearches}: title {Title}, author {Author}, keywords {Keywords}.",
            SearchCount,
            MaximumSearches,
            normalizedTitle,
            normalizedAuthor,
            normalizedKeywords is null ? null : string.Join(", ", normalizedKeywords));

        var books = await _bookProvider.SearchAsync(
            new BookSearchQuery(normalizedTitle, normalizedAuthor, normalizedKeywords),
            cancellationToken);
        var candidates = books.Select(GetOrAddCandidate).ToArray();

        _logger.LogInformation(
            "Book finder tool search {SearchNumber} returned {BookCount} candidates ({UniqueBookCount} unique in this request).",
            SearchCount,
            candidates.Length,
            _booksByCandidateId.Count);

        return new BookSearchToolResult(candidates);
    }

    public bool ContainsCandidate(string candidateId) =>
        _booksByCandidateId.ContainsKey(candidateId);

    public IReadOnlyList<RankedBook> Resolve(BookFinderCompletion completion) =>
        completion.RankedBooks
            .Select(ranking => new RankedBook(
                ranking.Score,
                ranking.Reason,
                _booksByCandidateId[ranking.CandidateId]))
            .ToArray();

    private BookSearchCandidate GetOrAddCandidate(Book book)
    {
        var identity = GetBookIdentity(book);

        if (!_candidateIdsByBook.TryGetValue(identity, out var candidateId))
        {
            candidateId = $"book-{++_nextCandidateId:D3}";
            _candidateIdsByBook.Add(identity, candidateId);
            _booksByCandidateId.Add(candidateId, book);
        }

        return new BookSearchCandidate(
            candidateId,
            book.Title,
            book.Author,
            book.FirstPublishYear,
            book.Description);
    }

    private static string GetBookIdentity(Book book) =>
        !string.IsNullOrWhiteSpace(book.BookKey)
            ? $"key:{book.BookKey.Trim().ToUpperInvariant()}"
            : $"metadata:{book.Title.Trim().ToUpperInvariant()}\u001f{book.Author.Trim().ToUpperInvariant()}\u001f{book.FirstPublishYear}";

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

internal sealed record BookSearchToolResult(
    [property: Description("Candidate books returned by Open Library.")]
    IReadOnlyList<BookSearchCandidate> Books);

internal sealed record BookSearchCandidate(
    [property: Description("Opaque ID to use when ranking this exact candidate.")]
    string CandidateId,
    string Title,
    string Author,
    int? FirstPublishYear,
    string Description);
