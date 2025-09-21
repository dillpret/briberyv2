using Bribery.Domain.Models;

namespace Bribery.Domain;

public sealed class GameService
{
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private readonly object _lock = new();
    private readonly Dictionary<Guid, Game> _games = new();
    private readonly Dictionary<string, Guid> _codes = new(StringComparer.OrdinalIgnoreCase);
    private readonly ITimeProvider _timeProvider;
    private readonly PromptLibrary _promptLibrary;
    private readonly RandomBribeLibrary _randomBribeLibrary;
    private readonly Random _codeRandom = new(98765);

    public GameService(ITimeProvider timeProvider, PromptLibrary promptLibrary, RandomBribeLibrary randomBribeLibrary)
    {
        _timeProvider = timeProvider;
        _promptLibrary = promptLibrary;
        _randomBribeLibrary = randomBribeLibrary;
    }

    public GameState CreateGame(string hostName, GameSettings? settings = null)
    {
        if (string.IsNullOrWhiteSpace(hostName))
        {
            throw new GameRuleException("Host name is required.");
        }

        var trimmed = hostName.Trim();
        settings ??= GameSettings.Default;
        settings.Validate();

        lock (_lock)
        {
            var code = GenerateCode();
            var host = new Player(trimmed, isHost: true, joinOrder: 0);
            var game = new Game(code, settings);
            game.Players.Add(host);
            _games.Add(game.Id, game);
            _codes.Add(code, game.Id);
            return CreateSnapshot(game);
        }
    }

    public PlayerState JoinGame(string code, string playerName, Guid? existingPlayerId = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new GameRuleException("Game code is required.");
        }

        if (string.IsNullOrWhiteSpace(playerName) && existingPlayerId is null)
        {
            throw new GameRuleException("Player name is required.");
        }

        lock (_lock)
        {
            if (!_codes.TryGetValue(code.Trim(), out var gameId))
            {
                throw new GameRuleException("Game not found.");
            }

            var game = _games[gameId];
            Player? player = null;
            if (existingPlayerId is not null)
            {
                player = game.Players.FirstOrDefault(p => p.Id == existingPlayerId);
            }

            if (player is null && !string.IsNullOrWhiteSpace(playerName))
            {
                player = game.Players.FirstOrDefault(p => string.Equals(p.Name, playerName.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (player is null)
            {
                player = new Player(playerName!.Trim(), isHost: false, joinOrder: game.Players.Count)
                {
                    IsWaiting = game.Phase is not GamePhase.Lobby,
                };
                game.Players.Add(player);
            }
            else
            {
                player.Name = playerName?.Trim() ?? player.Name;
            }

            player.IsConnected = true;
            return player.ToPublicState();
        }
    }

    public GameState GetGame(Guid id)
    {
        lock (_lock)
        {
            if (!_games.TryGetValue(id, out var game))
            {
                throw new GameRuleException("Game not found.");
            }

            ApplyTimerTransitions(game);
            return CreateSnapshot(game);
        }
    }

    public GameState GetGameByCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new GameRuleException("Game code is required.");
        }

        lock (_lock)
        {
            if (!_codes.TryGetValue(code.Trim(), out var gameId))
            {
                throw new GameRuleException("Game not found.");
            }

            var game = _games[gameId];
            ApplyTimerTransitions(game);
            return CreateSnapshot(game);
        }
    }

    public GameState StartGame(Guid gameId, Guid requestingPlayerId)
    {
        lock (_lock)
        {
            var game = GetGameInternal(gameId);
            var requester = game.Players.FirstOrDefault(p => p.Id == requestingPlayerId) ?? throw new GameRuleException("Player not part of this game.");
            if (!requester.IsHost)
            {
                throw new GameRuleException("Only the host can start the game.");
            }

            var activePlayers = game.Players.Where(p => !p.IsWaiting).ToList();
            if (activePlayers.Count < 3)
            {
                throw new GameRuleException("At least three active players are required to start the game.");
            }

            if (game.Phase != GamePhase.Lobby)
            {
                throw new GameRuleException("Game has already started.");
            }

            game.Settings.Validate();
            game.CurrentRoundNumber = 1;
            foreach (var player in game.Players)
            {
                player.IsWaiting = false;
            }

            game.CompletedRounds.Clear();
            BeginRound(game);
            return CreateSnapshot(game);
        }
    }

    public void ConfirmPrompt(Guid gameId, Guid playerId, PromptSelection selection)
    {
        lock (_lock)
        {
            var game = GetGameInternal(gameId);
            if (game.Phase != GamePhase.PromptSelection)
            {
                throw new GameRuleException("Prompt selection is not active.");
            }

            var round = game.ActiveRound ?? throw new InvalidOperationException("Round not initialised.");
            if (!round.PendingPromptConfirmations.Contains(playerId))
            {
                return;
            }

            if (selection.Source == PromptSource.Library && !_promptLibrary.Contains(selection.Text))
            {
                throw new GameRuleException("Prompt must come from the library when that source is selected.");
            }

            round.PromptsByTarget[playerId] = selection;
            round.PendingPromptConfirmations.Remove(playerId);
            if (round.PendingPromptConfirmations.Count == 0)
            {
                EnterSubmissionPhase(game);
            }
        }
    }

    public void SubmitBribe(Guid gameId, Guid playerId, Guid targetId, BribeSubmission submission)
    {
        lock (_lock)
        {
            var game = GetGameInternal(gameId);
            if (game.Phase != GamePhase.Submission)
            {
                throw new GameRuleException("Submissions are not being accepted right now.");
            }

            var round = game.ActiveRound ?? throw new InvalidOperationException("Round not active.");
            if (!round.Assignments.TryGetValue(playerId, out var targets) || !targets.Contains(targetId))
            {
                throw new GameRuleException("Target not assigned to player.");
            }

            if (!round.Submissions.TryGetValue(playerId, out var submissions))
            {
                submissions = new Dictionary<Guid, SubmissionEntry>();
                round.Submissions[playerId] = submissions;
            }

            submissions[targetId] = new SubmissionEntry(playerId, targetId, submission, isRandom: false);
            if (submissions.Count == round.Assignments[playerId].Count)
            {
                round.PendingSubmissions.Remove(playerId);
            }

            if (round.PendingSubmissions.Count == 0)
            {
                FinaliseSubmissions(game);
            }
        }
    }

    public void CastVote(Guid gameId, Guid voterId, Guid chosenBriberId)
    {
        lock (_lock)
        {
            var game = GetGameInternal(gameId);
            if (game.Phase != GamePhase.Voting)
            {
                throw new GameRuleException("Voting is not open.");
            }

            var round = game.ActiveRound ?? throw new InvalidOperationException("Round not active.");
            if (!round.BribesByTarget.TryGetValue(voterId, out var bribes))
            {
                throw new GameRuleException("Player has no bribes to vote on.");
            }

            if (!bribes.Any(b => b.SubmittedBy == chosenBriberId))
            {
                throw new GameRuleException("Selected bribe does not exist.");
            }

            round.Votes[voterId] = chosenBriberId;
            round.PendingVotes.Remove(voterId);
            if (round.PendingVotes.Count == 0)
            {
                CompleteVoting(game);
            }
        }
    }

    public void AdvanceFromScoreboard(Guid gameId, Guid requestingPlayerId)
    {
        lock (_lock)
        {
            var game = GetGameInternal(gameId);
            var requester = game.Players.FirstOrDefault(p => p.Id == requestingPlayerId) ?? throw new GameRuleException("Player not recognised.");
            if (game.Phase != GamePhase.Scoreboard)
            {
                throw new GameRuleException("Scoreboard is not active.");
            }

            if (!requester.IsHost)
            {
                throw new GameRuleException("Only the host can advance the game.");
            }

            AdvanceToNextPhaseOrFinish(game);
        }
    }

    public GameState UpdateSettings(Guid gameId, Guid hostId, GameSettings settings)
    {
        lock (_lock)
        {
            var game = GetGameInternal(gameId);
            var host = game.Players.FirstOrDefault(p => p.Id == hostId) ?? throw new GameRuleException("Player not recognised.");
            if (!host.IsHost)
            {
                throw new GameRuleException("Only the host can update settings.");
            }

            if (game.Phase != GamePhase.Lobby)
            {
                throw new GameRuleException("Settings can only be modified while in the lobby.");
            }

            settings.Validate();
            game.Settings = settings;
            return CreateSnapshot(game);
        }
    }

    public GameState RemovePlayer(Guid gameId, Guid hostId, Guid playerToRemoveId)
    {
        lock (_lock)
        {
            var game = GetGameInternal(gameId);
            var host = game.Players.FirstOrDefault(p => p.Id == hostId) ?? throw new GameRuleException("Player not recognised.");
            if (!host.IsHost)
            {
                throw new GameRuleException("Only the host can remove players.");
            }

            var target = game.Players.FirstOrDefault(p => p.Id == playerToRemoveId) ?? throw new GameRuleException("Player not recognised.");
            if (target.IsHost)
            {
                throw new GameRuleException("The host cannot remove themselves.");
            }

            game.Players.Remove(target);

            if (game.ActiveRound is null)
            {
                return CreateSnapshot(game);
            }

            var round = game.ActiveRound;
            round.ActivePlayers.Remove(target.Id);
            round.Assignments.Remove(target.Id);
            round.Submissions.Remove(target.Id);
            round.PromptsByTarget.Remove(target.Id);
            round.PendingPromptConfirmations.Remove(target.Id);
            round.PendingSubmissions.Remove(target.Id);
            round.PendingVotes.Remove(target.Id);
            round.Votes.Remove(target.Id);
            round.BribesByTarget.Remove(target.Id);

            foreach (var assignment in round.Assignments.Values)
            {
                assignment.Remove(target.Id);
            }

            foreach (var submissions in round.Submissions.Values)
            {
                submissions.Remove(target.Id);
            }

            foreach (var list in round.BribesByTarget.Values)
            {
                list.RemoveAll(b => b.SubmittedBy == target.Id || b.TargetId == target.Id);
            }

            if (round.ActivePlayers.Count < 3)
            {
                game.ActiveRound = null;
                game.Phase = GamePhase.Lobby;
                game.PhaseEndsAt = null;
                game.CurrentRoundNumber = 0;
                return CreateSnapshot(game);
            }

            round.Submissions.Clear();
            round.BribesByTarget.Clear();
            round.Votes.Clear();
            round.PendingVotes.Clear();

            PrepareAssignments(game, round);

            if (game.Settings.CustomPromptsEnabled)
            {
                round.PromptsByTarget.Clear();
                round.PendingPromptConfirmations = new HashSet<Guid>(round.ActivePlayers);
                game.Phase = GamePhase.PromptSelection;
                game.PhaseEndsAt = CalculatePhaseEnd(game.Settings.PromptSelectionTimerSeconds);
            }
            else
            {
                round.PromptsByTarget.Clear();
                EnterSubmissionPhase(game);
            }

            return CreateSnapshot(game);
        }
    }

    public PlayerState UpdateConnection(Guid gameId, Guid playerId, bool isConnected)
    {
        lock (_lock)
        {
            var game = GetGameInternal(gameId);
            var player = game.Players.FirstOrDefault(p => p.Id == playerId) ?? throw new GameRuleException("Player not recognised.");
            player.IsConnected = isConnected;
            return player.ToPublicState();
        }
    }

    public void Tick(Guid gameId)
    {
        lock (_lock)
        {
            var game = GetGameInternal(gameId);
            ApplyTimerTransitions(game);
        }
    }

    public void TickAll()
    {
        lock (_lock)
        {
            foreach (var game in _games.Values)
            {
                ApplyTimerTransitions(game);
            }
        }
    }

    private void ApplyTimerTransitions(Game game)
    {
        if (game.Phase is GamePhase.Lobby or GamePhase.Finished)
        {
            return;
        }

        if (game.PhaseEndsAt is null || game.PhaseEndsAt > _timeProvider.UtcNow)
        {
            return;
        }

        switch (game.Phase)
        {
            case GamePhase.PromptSelection:
                AutoCompletePrompts(game);
                break;
            case GamePhase.Submission:
                FinaliseSubmissions(game);
                break;
            case GamePhase.Voting:
                AutoCompleteVotes(game);
                break;
            case GamePhase.Scoreboard:
                AdvanceToNextPhaseOrFinish(game);
                break;
        }
    }

    private void AutoCompletePrompts(Game game)
    {
        var round = game.ActiveRound ?? throw new InvalidOperationException();
        foreach (var pending in round.PendingPromptConfirmations.ToList())
        {
            var prompt = new PromptSelection(_promptLibrary.GetRandomPrompt(), PromptSource.Random);
            round.PromptsByTarget[pending] = prompt;
            round.PendingPromptConfirmations.Remove(pending);
        }

        EnterSubmissionPhase(game);
    }

    private void FinaliseSubmissions(Game game)
    {
        var round = game.ActiveRound ?? throw new InvalidOperationException();
        foreach (var (playerId, targets) in round.Assignments)
        {
            if (!round.Submissions.TryGetValue(playerId, out var submissions))
            {
                submissions = new Dictionary<Guid, SubmissionEntry>();
                round.Submissions[playerId] = submissions;
            }

            foreach (var target in targets)
            {
                if (!submissions.ContainsKey(target))
                {
                    submissions[target] = new SubmissionEntry(playerId, target, _randomBribeLibrary.CreateRandomBribe(), true);
                }
            }
        }

        round.BribesByTarget.Clear();
        foreach (var submissions in round.Submissions.Values)
        {
            foreach (var entry in submissions.Values)
            {
                if (!round.BribesByTarget.TryGetValue(entry.TargetId, out var list))
                {
                    list = new List<BribeForTarget>();
                    round.BribesByTarget[entry.TargetId] = list;
                }

                list.Add(new BribeForTarget(entry.BriberId, entry.TargetId, entry.Submission, entry.IsRandom));
            }
        }

        foreach (var list in round.BribesByTarget.Values)
        {
            list.Sort((a, b) => string.Compare(a.SubmittedBy.ToString(), b.SubmittedBy.ToString(), StringComparison.Ordinal));
        }

        round.PendingVotes = new HashSet<Guid>(round.BribesByTarget.Keys);
        round.PendingSubmissions.Clear();
        round.Votes.Clear();
        game.Phase = GamePhase.Voting;
        game.PhaseEndsAt = CalculatePhaseEnd(game.Settings.VotingTimerSeconds);
    }

    private void AutoCompleteVotes(Game game)
    {
        var round = game.ActiveRound ?? throw new InvalidOperationException();
        foreach (var pending in round.PendingVotes.ToList())
        {
            var bribe = round.BribesByTarget[pending].First();
            round.Votes[pending] = bribe.SubmittedBy;
            round.PendingVotes.Remove(pending);
        }

        CompleteVoting(game);
    }

    private void CompleteVoting(Game game)
    {
        var round = game.ActiveRound ?? throw new InvalidOperationException();
        var roundScores = new Dictionary<Guid, double>();
        foreach (var player in game.Players)
        {
            roundScores[player.Id] = 0;
        }

        foreach (var (target, briber) in round.Votes)
        {
            var bribe = round.BribesByTarget[target].First(b => b.SubmittedBy == briber);
            var points = bribe.IsRandom ? 0.5 : 1.0;
            roundScores[briber] += points;
            var player = game.Players.First(p => p.Id == briber);
            player.Score += points;
        }

        var scoreboard = roundScores.Select(pair => new PlayerScoreDelta(pair.Key, pair.Value, game.Players.First(p => p.Id == pair.Key).Score))
            .OrderByDescending(p => p.TotalScore)
            .ToList();

        var promptResults = round.BribesByTarget.Select(entry =>
        {
            var target = entry.Key;
            var prompt = round.PromptsByTarget[target];
            var winningBriber = round.Votes.ContainsKey(target) ? round.Votes[target] : entry.Value.First().SubmittedBy;
            var bribe = entry.Value.First(b => b.SubmittedBy == winningBriber);
            return new PromptResult(target, prompt.Text, winningBriber, bribe.IsRandom);
        }).ToList();

        game.CompletedRounds.Add(new RoundSummary(round.Number, scoreboard, promptResults));
        game.Phase = GamePhase.Scoreboard;
        game.PhaseEndsAt = CalculatePhaseEnd(game.Settings.ResultsTimerSeconds);
    }

    private void AdvanceToNextPhaseOrFinish(Game game)
    {
        var round = game.ActiveRound ?? throw new InvalidOperationException();
        game.ActiveRound = null;
        game.PhaseEndsAt = null;

        if (game.CurrentRoundNumber >= game.Settings.TotalRounds)
        {
            game.Phase = GamePhase.Finished;
            return;
        }

        game.CurrentRoundNumber++;
        BeginRound(game);
    }

    private void BeginRound(Game game)
    {
        var activePlayers = game.Players.Where(p => !p.IsWaiting).ToList();
        if (activePlayers.Count < 3)
        {
            throw new GameRuleException("At least three active players are required to play a round.");
        }

        var round = new RoundData(game.CurrentRoundNumber, activePlayers.Select(p => p.Id).ToList(), game.Settings.CustomPromptsEnabled);
        game.ActiveRound = round;

        foreach (var player in game.Players)
        {
            player.IsWaiting = false;
        }

        PrepareAssignments(game, round);

        if (game.Settings.CustomPromptsEnabled)
        {
            round.PendingPromptConfirmations = new HashSet<Guid>(round.ActivePlayers);
            game.Phase = GamePhase.PromptSelection;
            game.PhaseEndsAt = CalculatePhaseEnd(game.Settings.PromptSelectionTimerSeconds);
        }
        else
        {
            foreach (var target in round.ActivePlayers)
            {
                var prompt = new PromptSelection(_promptLibrary.GetRandomPrompt(), PromptSource.Library);
                round.PromptsByTarget[target] = prompt;
            }

            EnterSubmissionPhase(game);
        }
    }

    private void PrepareAssignments(Game game, RoundData round)
    {
        round.Assignments.Clear();
        var orderedPlayers = round.ActivePlayers
            .Select(id => game.Players.First(p => p.Id == id))
            .OrderBy(p => p.JoinOrder)
            .ToList();

        var ids = orderedPlayers.Select(p => p.Id).ToList();
        if (ids.Count < 3)
        {
            throw new GameRuleException("At least three players are required for assignments.");
        }

        var offset = (round.Number - 1) % ids.Count;
        if (offset > 0)
        {
            ids = ids.Skip(offset).Concat(ids.Take(offset)).ToList();
        }

        for (var i = 0; i < ids.Count; i++)
        {
            var briberId = ids[i];
            var firstTarget = ids[(i + 1) % ids.Count];
            var secondTarget = ids[(i + 2) % ids.Count];
            round.Assignments[briberId] = new List<Guid> { firstTarget, secondTarget };
            var player = game.Players.First(p => p.Id == briberId);
            player.PastTargets.Add(firstTarget);
            player.PastTargets.Add(secondTarget);
        }

        round.PendingSubmissions = new HashSet<Guid>(round.Assignments.Keys);
    }

    private void EnterSubmissionPhase(Game game)
    {
        var round = game.ActiveRound ?? throw new InvalidOperationException();
        if (round.Assignments.Count == 0)
        {
            PrepareAssignments(game, round);
        }

        if (round.PromptsByTarget.Count == 0)
        {
            foreach (var target in round.ActivePlayers)
            {
                var prompt = new PromptSelection(_promptLibrary.GetRandomPrompt(), PromptSource.Random);
                round.PromptsByTarget[target] = prompt;
            }
        }

        round.PendingPromptConfirmations.Clear();
        game.Phase = GamePhase.Submission;
        game.PhaseEndsAt = CalculatePhaseEnd(game.Settings.SubmissionTimerSeconds);
    }

    private DateTimeOffset? CalculatePhaseEnd(int seconds)
    {
        return seconds > 0 ? _timeProvider.UtcNow.AddSeconds(seconds) : null;
    }

    private Game GetGameInternal(Guid id)
    {
        if (!_games.TryGetValue(id, out var game))
        {
            throw new GameRuleException("Game not found.");
        }

        return game;
    }

    private string GenerateCode()
    {
        while (true)
        {
            var chars = Enumerable.Range(0, 4).Select(_ => CodeAlphabet[_codeRandom.Next(CodeAlphabet.Length)]);
            var code = string.Concat(chars);
            if (!_codes.ContainsKey(code))
            {
                return code;
            }
        }
    }

    private GameState CreateSnapshot(Game game)
    {
        var roundSnapshot = game.ActiveRound is null ? null : CreateRoundSnapshot(game.ActiveRound);
        return new GameState(
            game.Id,
            game.Code,
            game.Phase,
            game.Settings,
            game.CurrentRoundNumber,
            game.Players.Select(p => p.ToPublicState()).ToList(),
            roundSnapshot,
            game.CompletedRounds.ToList(),
            game.PhaseEndsAt
        );
    }

    private static RoundSnapshot CreateRoundSnapshot(RoundData round)
    {
        return new RoundSnapshot(
            round.Number,
            round.Assignments.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<Guid>)kvp.Value.ToList()),
            round.Submissions.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<BribeRecord>)kvp.Value.Values.Select(entry => new BribeRecord(entry.TargetId, entry.Submission, entry.IsRandom)).ToList()),
            round.BribesByTarget.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<BribeForTarget>)kvp.Value.ToList()),
            new HashSet<Guid>(round.PendingPromptConfirmations),
            new HashSet<Guid>(round.PendingSubmissions),
            new HashSet<Guid>(round.PendingVotes),
            round.PromptsByTarget.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        );
    }

    private sealed class Game
    {
        public Game(string code, GameSettings settings)
        {
            Code = code;
            Settings = settings;
        }

        public Guid Id { get; } = Guid.NewGuid();
        public string Code { get; }
        public GameSettings Settings { get; set; }
        public GamePhase Phase { get; set; } = GamePhase.Lobby;
        public int CurrentRoundNumber { get; set; }
        public RoundData? ActiveRound { get; set; }
        public List<Player> Players { get; } = new();
        public List<RoundSummary> CompletedRounds { get; } = new();
        public DateTimeOffset? PhaseEndsAt { get; set; }
    }

    private sealed class Player
    {
        public Player(string name, bool isHost, int joinOrder)
        {
            Id = Guid.NewGuid();
            Name = name;
            IsHost = isHost;
            JoinOrder = joinOrder;
        }

        public Guid Id { get; }
        public string Name { get; set; }
        public bool IsHost { get; }
        public bool IsConnected { get; set; }
        public bool IsWaiting { get; set; }
        public double Score { get; set; }
        public int JoinOrder { get; }
        public HashSet<Guid> PastTargets { get; } = new();

        public PlayerState ToPublicState() => new(Id, Name, IsHost, IsConnected, Math.Round(Score, 2), IsWaiting);
    }

    private sealed class RoundData
    {
        public RoundData(int number, IReadOnlyList<Guid> activePlayers, bool customPrompts)
        {
            Number = number;
            ActivePlayers = activePlayers.ToList();
            CustomPrompts = customPrompts;
        }

        public int Number { get; }
        public List<Guid> ActivePlayers { get; }
        public bool CustomPrompts { get; }
        public Dictionary<Guid, List<Guid>> Assignments { get; } = new();
        public Dictionary<Guid, Dictionary<Guid, SubmissionEntry>> Submissions { get; } = new();
        public Dictionary<Guid, List<BribeForTarget>> BribesByTarget { get; } = new();
        public HashSet<Guid> PendingPromptConfirmations { get; set; } = new();
        public HashSet<Guid> PendingSubmissions { get; set; } = new();
        public HashSet<Guid> PendingVotes { get; set; } = new();
        public Dictionary<Guid, PromptSelection> PromptsByTarget { get; } = new();
        public Dictionary<Guid, Guid> Votes { get; } = new();
    }

    private sealed class SubmissionEntry
    {
        public SubmissionEntry(Guid briberId, Guid targetId, BribeSubmission submission, bool isRandom)
        {
            BriberId = briberId;
            TargetId = targetId;
            Submission = submission;
            IsRandom = isRandom;
        }

        public Guid BriberId { get; }
        public Guid TargetId { get; }
        public BribeSubmission Submission { get; }
        public bool IsRandom { get; }
    }
}
