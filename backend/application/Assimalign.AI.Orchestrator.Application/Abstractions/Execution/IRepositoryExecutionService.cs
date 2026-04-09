using Assimalign.AI.Orchestrator.Core.Models;

namespace Assimalign.AI.Orchestrator.Application.Abstractions.Execution;

public interface IRepositoryExecutionService
{
    Task<RepositoryExecutionResult> ExecuteAsync(
        ConversationInput input,
        RepositoryTarget repository,
        OrchestrationResult orchestration,
        IReadOnlyList<ThreadMessage>? threadHistory,
        CancellationToken cancellationToken = default);
}
