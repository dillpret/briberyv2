namespace Bribery.Api.Contracts;

public sealed record KickPlayerRequest(Guid HostId, Guid PlayerId);
