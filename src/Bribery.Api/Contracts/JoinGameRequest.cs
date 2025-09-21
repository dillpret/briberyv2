namespace Bribery.Api.Contracts;

public sealed record JoinGameRequest(string Name, Guid? PlayerId);
