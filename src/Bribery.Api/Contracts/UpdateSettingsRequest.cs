using Bribery.Domain.Models;

namespace Bribery.Api.Contracts;

public sealed record UpdateSettingsRequest(Guid PlayerId, GameSettings Settings);
