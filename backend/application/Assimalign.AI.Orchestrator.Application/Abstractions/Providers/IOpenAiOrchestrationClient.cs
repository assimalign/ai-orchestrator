using Assimalign.AI.Orchestrator.Core.Models;

namespace Assimalign.AI.Orchestrator.Application.Abstractions.Providers;

public interface IOpenAiOrchestrationClient
{
    string DefaultModel { get; }

    Task<PlanningArtifact> CreatePlanAsync(
        string requirement,
        GitHubContextSnapshot? context,
        IReadOnlyList<ThreadMessage>? threadHistory,
        string? modelOverride = null,
        CancellationToken cancellationToken = default);

    Task<string> RespondToReviewAsync(
        string requirement,
        PlanningArtifact plan,
        ReviewArtifact review,
        GitHubContextSnapshot? context,
        IReadOnlyList<ThreadMessage>? threadHistory,
        string? modelOverride = null,
        CancellationToken cancellationToken = default);

    Task<string> SynthesizeBriefAsync(
        string requirement,
        PlanningArtifact plan,
        ReviewArtifact review,
        string? codexDebateReply,
        GitHubContextSnapshot? context,
        IReadOnlyList<ThreadMessage>? threadHistory,
        string? modelOverride = null,
        CancellationToken cancellationToken = default);

    Task<RepositoryExecutionContextArtifact> CreateExecutionContextAsync(
        string requirement,
        OrchestrationResult orchestration,
        string repositoryTree,
        string executionEnvironment,
        IReadOnlyList<ThreadMessage>? threadHistory,
        string? modelOverride = null,
        CancellationToken cancellationToken = default);

    Task<RepositoryExecutionArtifact> CreateExecutionArtifactAsync(
        string requirement,
        OrchestrationResult orchestration,
        RepositoryExecutionContextArtifact executionContext,
        string repositoryTree,
        string executionEnvironment,
        IReadOnlyDictionary<string, string> fileContents,
        IReadOnlyList<ThreadMessage>? threadHistory,
        string? modelOverride = null,
        CancellationToken cancellationToken = default);
}
