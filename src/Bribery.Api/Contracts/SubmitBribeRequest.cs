namespace Bribery.Api.Contracts;

public sealed record SubmitBribeRequest(Guid PlayerId, Guid TargetId, string Type, string Content);
