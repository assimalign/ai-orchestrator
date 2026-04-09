using Assimalign.AI.Orchestrator.Core.Models;

namespace Assimalign.AI.Orchestrator.Application.Abstractions.Execution;

public interface IRepositoryExecutionService
{
    Task<RepositoryInspectionResult> InspectAsync(
        ConversationInput input,
        RepositoryTarget repository,
        OrchestrationResult orchestration,
        IReadOnlyList<ThreadMessage>? threadHistory,
        CancellationToken cancellationToken = default);

    Task<RepositoryExecutionResult> ExecuteAsync(
        ConversationInput input,
        RepositoryTarget repository,
        OrchestrationResult orchestration,
        IReadOnlyList<ThreadMessage>? threadHistory,
        CancellationToken cancellationToken = default);
}
