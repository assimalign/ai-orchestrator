using Assimalign.AI.Orchestrator.Core.Models;

namespace Assimalign.AI.Orchestrator.Application.Abstractions.Providers;

public interface IAnthropicReviewClient
{
    string DefaultModel { get; }

    Task<ReviewArtifact> CritiquePlanAsync(
        string requirement,
        PlanningArtifact plan,
        GitHubContextSnapshot? context,
        IReadOnlyList<ThreadMessage>? threadHistory,
        string? modelOverride = null,
        CancellationToken cancellationToken = default);
}
