using Bribery.Domain;
using Bribery.Domain.Models;

namespace Bribery.Domain.Tests;

public class GameServiceTests
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly PromptLibrary _promptLibrary = new([
        "Convince them to give you their dessert",
        "Offer to babysit their dragon",
        "Promise to do their chores for a year"
    ]);
    private readonly RandomBribeLibrary _randomBribes = new([
        "singing platypus",
        "glittery marshmallows"
    ], [
        "moonwalking",
        "building sandcastles"
    ]);

    [Fact]
    public void CreateGame_AssignsHostAndLobbyState()
    {
        var service = CreateService();

        var result = service.CreateGame("Alice");

        Assert.Equal(GamePhase.Lobby, result.Phase);
        var host = Assert.Single(result.Players);
        Assert.True(host.IsHost);
        Assert.Equal("Alice", host.Name);
        Assert.Equal(4, result.Code.Length);
        Assert.All(result.Code, c => char.IsLetterOrDigit(c));
    }

    [Fact]
    public void JoinGame_AddsPlayerWithPersistentId()
    {
        var service = CreateService();
        var game = service.CreateGame("Alice");

        var bob = service.JoinGame(game.Code, "Bob");

        Assert.False(bob.IsHost);
        Assert.Equal("Bob", bob.Name);
        var state = service.GetGame(game.Id);
        Assert.Equal(2, state.Players.Count);
        Assert.Contains(state.Players, p => p.Id == bob.Id);
    }

    [Fact]
    public void StartGame_RequiresThreePlayers()
    {
        var service = CreateService();
        var game = service.CreateGame("Alice");
        service.JoinGame(game.Code, "Bob");

        var ex = Assert.Throws<GameRuleException>(() => service.StartGame(game.Id, game.Players.Single().Id));

        Assert.Equal("At least three active players are required to start the game.", ex.Message);
    }

    [Fact]
    public void StartGame_BeginsPromptSelectionWhenCustomPromptsEnabled()
    {
        var service = CreateService();
        var game = service.CreateGame("Alice", GameSettings.Default with { CustomPromptsEnabled = true });
        var bob = service.JoinGame(game.Code, "Bob");
        var cara = service.JoinGame(game.Code, "Cara");

        var updated = service.StartGame(game.Id, game.Players.Single().Id);

        Assert.Equal(GamePhase.PromptSelection, updated.Phase);
        Assert.Equal(1, updated.CurrentRound);
        Assert.NotNull(updated.Round);
        Assert.Equal(3, updated.Round!.PendingPromptConfirmations.Count);
    }

    [Fact]
    public void PromptSelection_AdvancesWhenAllPlayersConfirm()
    {
        var service = CreateService();
        var game = service.CreateGame("Host", GameSettings.Default with { CustomPromptsEnabled = true });
        var bob = service.JoinGame(game.Code, "Bob");
        var cara = service.JoinGame(game.Code, "Cara");
        service.StartGame(game.Id, game.Players.Single().Id);

        foreach (var player in service.GetGame(game.Id).Players)
        {
            service.ConfirmPrompt(game.Id, player.Id, new PromptSelection("Convince them to give you their dessert", PromptSource.Library));
        }

        var after = service.GetGame(game.Id);
        Assert.Equal(GamePhase.Submission, after.Phase);
        Assert.NotNull(after.Round);
        Assert.All(after.Round!.Assignments, kvp =>
        {
            Assert.Equal(2, kvp.Value.Count);
            Assert.Equal(2, kvp.Value.Distinct().Count());
            Assert.DoesNotContain(kvp.Key, kvp.Value);
        });
        var targetCounts = new Dictionary<Guid, int>();
        foreach (var assignment in after.Round.Assignments)
        {
            foreach (var target in assignment.Value)
            {
                targetCounts[target] = targetCounts.TryGetValue(target, out var existing) ? existing + 1 : 1;
            }
        }
        Assert.All(after.Round.Assignments.Keys, id => Assert.Equal(2, targetCounts[id]));
    }

    [Fact]
    public void Submission_RecordsBribesAndAutoFillsMissing()
    {
        var service = CreateService();
        var game = service.CreateGame("Host", GameSettings.Default);
        var bob = service.JoinGame(game.Code, "Bob");
        var cara = service.JoinGame(game.Code, "Cara");
        var dave = service.JoinGame(game.Code, "Dave");
        service.StartGame(game.Id, game.Players.Single().Id);

        var round = service.GetGame(game.Id).Round!;
        foreach (var assignment in round.Assignments)
        {
            var playerId = assignment.Key;
            foreach (var target in assignment.Value.Take(1))
            {
                service.SubmitBribe(game.Id, playerId, target, BribeSubmission.FromText($"Bribe from {playerId} to {target}"));
            }
        }

        // fast-forward timer to force completion
        _timeProvider.Advance(TimeSpan.FromMinutes(2));
        service.Tick(game.Id);

        var after = service.GetGame(game.Id);
        Assert.Equal(GamePhase.Voting, after.Phase);
        Assert.All(after.Round!.Submissions.Values.SelectMany(s => s), s => Assert.False(string.IsNullOrWhiteSpace(s.Content.Content)));
        Assert.Equal(2, after.Round!.Submissions.Values.First().Count);
    }

    [Fact]
    public void Voting_AwardsPointsAndAdvancesToScoreboard()
    {
        var service = CreateService();
        var game = service.CreateGame("Host", GameSettings.Default with { SubmissionTimerSeconds = 0, VotingTimerSeconds = 0 });
        var bob = service.JoinGame(game.Code, "Bob");
        var cara = service.JoinGame(game.Code, "Cara");
        service.StartGame(game.Id, game.Players.Single().Id);

        var state = service.GetGame(game.Id);
        foreach (var assignment in state.Round!.Assignments)
        {
            foreach (var target in assignment.Value)
            {
                service.SubmitBribe(game.Id, assignment.Key, target, BribeSubmission.FromText($"Bribe {assignment.Key}->{target}"));
            }
        }

        var submissionComplete = service.GetGame(game.Id);
        foreach (var vote in submissionComplete.Round!.BribesByTarget)
        {
            var targetId = vote.Key;
            var bribes = vote.Value;
            var winning = bribes.First();
            service.CastVote(game.Id, targetId, winning.SubmittedBy);
        }

        var scoreboardState = service.GetGame(game.Id);
        Assert.Equal(GamePhase.Scoreboard, scoreboardState.Phase);
        Assert.All(scoreboardState.Players, p => Assert.True(p.Score >= 0));
    }

    [Fact]
    public void Scoreboard_CompletesRoundAndEndsGameAfterFinalRound()
    {
        var settings = GameSettings.Default with { TotalRounds = 1, SubmissionTimerSeconds = 0, VotingTimerSeconds = 0, ResultsTimerSeconds = 0 };
        var service = CreateService();
        var game = service.CreateGame("Host", settings);
        var bob = service.JoinGame(game.Code, "Bob");
        var cara = service.JoinGame(game.Code, "Cara");
        service.StartGame(game.Id, game.Players.Single().Id);

        var state = service.GetGame(game.Id);
        foreach (var assignment in state.Round!.Assignments)
        {
            foreach (var target in assignment.Value)
            {
                service.SubmitBribe(game.Id, assignment.Key, target, BribeSubmission.FromText("Bribe"));
            }
        }

        state = service.GetGame(game.Id);
        foreach (var target in state.Round!.BribesByTarget.Keys)
        {
            var firstBribe = state.Round.BribesByTarget[target].First();
            service.CastVote(game.Id, target, firstBribe.SubmittedBy);
        }

        var scoreboard = service.GetGame(game.Id);
        service.AdvanceFromScoreboard(game.Id, game.Players.Single().Id);

        var finished = service.GetGame(game.Id);
        Assert.Equal(GamePhase.Finished, finished.Phase);
        Assert.Equal(1, finished.CompletedRounds.Count);
    }

    [Fact]
    public void HostCanRemovePlayerDuringRound()
    {
        var service = CreateService();
        var game = service.CreateGame("Host");
        var bob = service.JoinGame(game.Code, "Bob");
        var cara = service.JoinGame(game.Code, "Cara");
        var dave = service.JoinGame(game.Code, "Dave");
        var hostId = game.Players.Single().Id;
        service.StartGame(game.Id, hostId);

        var result = service.RemovePlayer(game.Id, hostId, cara.Id);

        Assert.DoesNotContain(result.Players, p => p.Id == cara.Id);
        Assert.Equal(GamePhase.Submission, result.Phase);
        Assert.NotNull(result.Round);
        Assert.All(result.Round!.Assignments.Keys, id => Assert.NotEqual(cara.Id, id));
        Assert.All(result.Round.Assignments.Values.SelectMany(x => x), target => Assert.NotEqual(cara.Id, target));
    }

    private GameService CreateService()
    {
        return new GameService(_timeProvider, _promptLibrary, _randomBribes);
    }

    private sealed class FakeTimeProvider : ITimeProvider
    {
        private DateTimeOffset _current;

        public FakeTimeProvider(DateTimeOffset initial)
        {
            _current = initial;
        }

        public DateTimeOffset UtcNow => _current;

        public void Advance(TimeSpan span) => _current += span;
    }
}
