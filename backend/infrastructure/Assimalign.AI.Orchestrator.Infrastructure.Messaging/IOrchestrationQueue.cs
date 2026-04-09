using Assimalign.AI.Orchestrator.Core.Models;

namespace Assimalign.AI.Orchestrator.Infrastructure.Messaging;

public interface IOrchestrationQueue
{
    Task EnqueueAsync(OrchestrationJob job, CancellationToken cancellationToken = default);
}
