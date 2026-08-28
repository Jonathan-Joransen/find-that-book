namespace FindThatBook.Api.Prompts;

public readonly record struct PromptId(string Name, int Version)
{
    public override string ToString() => $"{Name}.v{Version}";
}
