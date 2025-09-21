namespace Bribery.Domain.Models;

public sealed record GameState(
    Guid Id,
    string Code,
    GamePhase Phase,
    GameSettings Settings,
    int CurrentRound,
    IReadOnlyList<PlayerState> Players,
    RoundSnapshot? Round,
    IReadOnlyList<RoundSummary> CompletedRounds,
    DateTimeOffset? PhaseEndsAt
);

public sealed record PlayerState(Guid Id, string Name, bool IsHost, bool IsConnected, double Score, bool IsWaiting);

public sealed record RoundSnapshot(
    int RoundNumber,
    IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> Assignments,
    IReadOnlyDictionary<Guid, IReadOnlyList<BribeRecord>> Submissions,
    IReadOnlyDictionary<Guid, IReadOnlyList<BribeForTarget>> BribesByTarget,
    IReadOnlySet<Guid> PendingPromptConfirmations,
    IReadOnlySet<Guid> PendingSubmissions,
    IReadOnlySet<Guid> PendingVotes,
    IReadOnlyDictionary<Guid, PromptSelection> PromptsByTarget
);

public sealed record BribeRecord(Guid TargetId, BribeSubmission Content, bool IsRandom);

public sealed record BribeForTarget(Guid SubmittedBy, Guid TargetId, BribeSubmission Content, bool IsRandom);

public sealed record RoundSummary(
    int RoundNumber,
    IReadOnlyList<PlayerScoreDelta> Scoreboard,
    IReadOnlyList<PromptResult> PromptResults
);

public sealed record PlayerScoreDelta(Guid PlayerId, double RoundPoints, double TotalScore);

public sealed record PromptResult(Guid TargetPlayerId, string Prompt, Guid WinningPlayerId, bool WasRandom);
