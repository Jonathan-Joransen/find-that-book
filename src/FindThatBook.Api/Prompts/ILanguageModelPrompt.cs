namespace FindThatBook.Api.Prompts;

public interface ILanguageModelPrompt<TResponse>
{
    PromptId Id { get; }

    string SystemMessage { get; }

    string UserMessage { get; }

    LanguageModelSettings Settings { get; }

    void Validate(TResponse response);
}
