using Bribery.Domain.Models;

namespace Bribery.Domain;

public sealed class PromptLibrary
{
    private readonly IReadOnlyList<string> _prompts;
    private readonly Random _random;

    public PromptLibrary(IEnumerable<string> prompts)
    {
        _prompts = prompts.Distinct().Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToArray();
        if (_prompts.Count == 0)
        {
            throw new ArgumentException("Prompt library cannot be empty.", nameof(prompts));
        }

        _random = new Random(1234);
    }

    public string GetRandomPrompt()
    {
        return _prompts[_random.Next(_prompts.Count)];
    }

    public bool Contains(string prompt) => _prompts.Contains(prompt);

    public IReadOnlyList<string> All => _prompts;
}
