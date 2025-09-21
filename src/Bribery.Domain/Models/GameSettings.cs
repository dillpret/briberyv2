namespace Bribery.Domain.Models;

public record GameSettings(
    int TotalRounds = 3,
    int PromptSelectionTimerSeconds = 45,
    int SubmissionTimerSeconds = 75,
    int VotingTimerSeconds = 60,
    int ResultsTimerSeconds = 30,
    bool CustomPromptsEnabled = false)
{
    public static GameSettings Default { get; } = new();

    public void Validate()
    {
        if (TotalRounds is < 1 or > 100)
        {
            throw new GameRuleException("Rounds must be between 1 and 100.");
        }

        ValidateTimer(PromptSelectionTimerSeconds, "prompt selection");
        ValidateTimer(SubmissionTimerSeconds, "submission");
        ValidateTimer(VotingTimerSeconds, "voting");
        ValidateTimer(ResultsTimerSeconds, "results");
    }

    private static void ValidateTimer(int value, string name)
    {
        if (value is < 0 or > 600)
        {
            throw new GameRuleException($"The {name} timer must be between 0 and 600 seconds.");
        }
    }
}
