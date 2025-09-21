using Bribery.Domain.Models;

namespace Bribery.Api.Contracts;

public sealed record JoinGameResponse(PlayerState Player, GameState Game);
