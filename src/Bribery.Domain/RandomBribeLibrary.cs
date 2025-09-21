using Bribery.Domain.Models;

namespace Bribery.Domain;

public sealed class RandomBribeLibrary
{
    private readonly IReadOnlyList<string> _subjects;
    private readonly IReadOnlyList<string> _activities;
    private readonly Random _random;

    public RandomBribeLibrary(IEnumerable<string> subjects, IEnumerable<string> activities)
    {
        _subjects = subjects.Distinct().Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToArray();
        _activities = activities.Distinct().Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).ToArray();
        if (_subjects.Count == 0 || _activities.Count == 0)
        {
            throw new ArgumentException("Random bribe pools cannot be empty.");
        }

        _random = new Random(4321);
    }

    public BribeSubmission CreateRandomBribe()
    {
        var subject = _subjects[_random.Next(_subjects.Count)];
        var activity = _activities[_random.Next(_activities.Count)];
        return BribeSubmission.FromText($"{subject} while {activity}");
    }
}
