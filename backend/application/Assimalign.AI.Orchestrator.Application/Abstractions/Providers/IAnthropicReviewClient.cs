using Assimalign.AI.Orchestrator.Core.Models;

namespace Assimalign.AI.Orchestrator.Application.Abstractions.Providers;

public interface IAnthropicReviewClient
{
    string DefaultModel { get; }

    Task<T> GenerateStructuredAsync<T>(
        ProviderPromptRequest request,
        CancellationToken cancellationToken = default);

    Task<string> GenerateTextAsync(
        ProviderPromptRequest request,
        CancellationToken cancellationToken = default);
}
