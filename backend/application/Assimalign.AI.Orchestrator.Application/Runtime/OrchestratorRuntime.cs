using Assimalign.AI.Orchestrator.Application.Configuration;
using Assimalign.AI.Orchestrator.Application.Abstractions.GitHub;
using Assimalign.AI.Orchestrator.Application.Abstractions.Providers;
using Assimalign.AI.Orchestrator.Application.Services;
using Assimalign.AI.Orchestrator.Core.Models;
using Assimalign.AI.Orchestrator.Infrastructure.Messaging;
using Assimalign.AI.Orchestrator.Infrastructure.Storage;

namespace Assimalign.AI.Orchestrator.Application.Runtime;

public sealed class OrchestratorRuntime
{
    public required OrchestratorSettings Settings { get; init; }
    public required ISecretProvider SecretProvider { get; init; }
    public required IThreadRepository Repository { get; init; }
    public required IGitHubContextService GitHubContextService { get; init; }
    public required ThreadConversationService ThreadService { get; init; }
    public required OrchestrationProcessor Processor { get; init; }
    public required ISpeechTokenService SpeechTokenService { get; init; }
    public required ProviderAvailability ProviderAvailability { get; init; }
    public IOrchestrationQueue? Queue { get; init; }
}
