namespace FindThatBook.Api.Providers.LanguageModelProviders;

public sealed class LanguageModelException : Exception
{
    public LanguageModelException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
