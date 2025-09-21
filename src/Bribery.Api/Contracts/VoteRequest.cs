namespace Bribery.Api.Contracts;

public sealed record VoteRequest(Guid PlayerId, Guid ChosenBriberId);
