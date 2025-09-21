using Bribery.Domain.Models;

namespace Bribery.Api.Contracts;

public sealed record CreateGameRequest(string HostName, GameSettings? Settings);
