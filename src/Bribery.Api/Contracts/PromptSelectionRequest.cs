using Bribery.Domain.Models;

namespace Bribery.Api.Contracts;

public sealed record PromptSelectionRequest(Guid PlayerId, string Prompt, PromptSource Source);
