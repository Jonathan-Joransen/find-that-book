using Microsoft.Extensions.AI;

namespace FindThatBook.Api.Prompts;

public interface ILanguageModelPrompt<TResponse>
{
    PromptId Id { get; }

    string SystemMessage { get; }

    string UserMessage { get; }

    LanguageModelSettings Settings { get; }

    IList<AITool>? Tools { get; }

    TResponse Normalize(TResponse response) => response;

    void Validate(TResponse response);
}
