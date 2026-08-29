namespace FindThatBook.Api.Providers;

public sealed class LanguageModelException : Exception
{
    public LanguageModelException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
