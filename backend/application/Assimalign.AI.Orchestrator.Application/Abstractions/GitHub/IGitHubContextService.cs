using Assimalign.AI.Orchestrator.Core.Models;

namespace Assimalign.AI.Orchestrator.Application.Abstractions.GitHub;

public interface IGitHubContextService
{
    Task<string?> GetAccessTokenForRepositoryOperationsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GitHubRepositoryReference>> ListRepositoriesAsync(
        CancellationToken cancellationToken = default);

    Task<GitHubContextSnapshot?> BuildSnapshotAsync(
        RepositoryTarget? target,
        CancellationToken cancellationToken = default);

    Task<GitHubBranchPreparationResult> EnsureWorkingBranchAsync(
        RepositoryTarget target,
        string? preferredBranchName,
        CancellationToken cancellationToken = default);

    Task<GitHubPromotionResult> PromoteBranchAsync(
        RepositoryTarget target,
        string commitMessage,
        CancellationToken cancellationToken = default);
}
