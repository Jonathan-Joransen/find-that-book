namespace FindThatBook.Api.Models.Requests;

public sealed record SearchBooksRequest(string? Query)
{
    public const int MinimumQueryLength = 1;

    public const int MaximumQueryLength = 500;
}
