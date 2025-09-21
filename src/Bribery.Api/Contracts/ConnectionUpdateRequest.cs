namespace Bribery.Api.Contracts;

public sealed record ConnectionUpdateRequest(Guid PlayerId, bool IsConnected);
