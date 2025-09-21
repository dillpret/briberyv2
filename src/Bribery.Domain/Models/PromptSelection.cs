namespace Bribery.Domain.Models;

public sealed record PromptSelection
{
    public string Text { get; }
    public PromptSource Source { get; }

    public PromptSelection(string text, PromptSource source)
    {
        if (source != PromptSource.Random && string.IsNullOrWhiteSpace(text))
        {
            throw new GameRuleException("Prompt text cannot be empty.");
        }

        if (text.Length > 200)
        {
            throw new GameRuleException("Prompts may not exceed 200 characters.");
        }

        Text = text;
        Source = source;
    }
}
