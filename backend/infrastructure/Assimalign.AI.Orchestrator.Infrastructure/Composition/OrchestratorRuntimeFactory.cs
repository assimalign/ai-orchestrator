using Assimalign.AI.Orchestrator.Application.Configuration;
using Assimalign.AI.Orchestrator.Application.Runtime;
using Assimalign.AI.Orchestrator.Application.Services;
using Assimalign.AI.Orchestrator.Core.Models;
using Assimalign.AI.Orchestrator.Infrastructure.Execution;
using Assimalign.AI.Orchestrator.Infrastructure.Integrations.AI;
using Assimalign.AI.Orchestrator.Infrastructure.Integrations.GitHub;
using Assimalign.AI.Orchestrator.Infrastructure.Messaging;
using Assimalign.AI.Orchestrator.Infrastructure.Messaging.ServiceBus;
using Assimalign.AI.Orchestrator.Infrastructure.Security;
using Assimalign.AI.Orchestrator.Infrastructure.Speech;
using Assimalign.AI.Orchestrator.Infrastructure.Storage;
using Assimalign.AI.Orchestrator.Infrastructure.Storage.Memory;
using Assimalign.AI.Orchestrator.Infrastructure.Storage.Tables;

namespace Assimalign.AI.Orchestrator.Infrastructure.Composition;

public static class OrchestratorRuntimeFactory
{
    public static async Task<OrchestratorRuntime> CreateAsync(
        OrchestratorSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (settings.UsesServiceBus && string.IsNullOrWhiteSpace(settings.ServiceBusConnectionString))
        {
            throw new InvalidOperationException(
                "EXECUTION_MODE is set to 'servicebus' but SERVICE_BUS_CONNECTION_STRING is missing.");
        }

        if (settings.UsesServiceBus && string.IsNullOrWhiteSpace(settings.AzureStorageConnectionString))
        {
            throw new InvalidOperationException(
                "EXECUTION_MODE is set to 'servicebus' but AZURE_STORAGE_CONNECTION_STRING is missing.");
        }

        var secretProvider = new SecretProvider(settings.KeyVaultUrl);
        var openAiApiKey = await secretProvider.GetAsync(
            settings.OpenAiApiKeySecretName,
            settings.OpenAiApiKey,
            cancellationToken);
        var anthropicApiKey = await secretProvider.GetAsync(
            settings.AnthropicApiKeySecretName,
            settings.AnthropicApiKey,
            cancellationToken);

        IThreadRepository repository =
            !string.IsNullOrWhiteSpace(settings.AzureStorageConnectionString)
                ? new TableThreadRepository(settings.AzureStorageConnectionString, settings.TableName)
                : new MemoryThreadRepository();

        await repository.InitializeAsync(cancellationToken);

        var queue = settings.UsesServiceBus && !string.IsNullOrWhiteSpace(settings.ServiceBusConnectionString)
            ? new ServiceBusOrchestrationQueue(settings.ServiceBusConnectionString, settings.ServiceBusQueueName)
            : null;

        var providerHttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(settings.ProviderHttpTimeoutSeconds),
        };
        var speechHttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(settings.SpeechHttpTimeoutSeconds),
        };
        var githubContextService = new GitHubContextService(settings, secretProvider);
        var openAiClient =
            !string.IsNullOrWhiteSpace(openAiApiKey)
                ? new OpenAiOrchestrationClient(providerHttpClient, openAiApiKey, settings.OpenAiModel)
                : null;
        var anthropicClient =
            !string.IsNullOrWhiteSpace(anthropicApiKey)
                ? new AnthropicReviewClient(providerHttpClient, anthropicApiKey, settings.AnthropicModel)
                : null;
        var engine = new OrchestrationEngine(
            settings.MaxConsensusRounds,
            githubContextService,
            openAiClient,
            anthropicClient);
        var repositoryExecutionService =
            openAiClient is null
                ? null
                : new RepositoryExecutionService(settings, openAiClient, githubContextService);
        var processor = new OrchestrationProcessor(
            repository,
            engine,
            githubContextService,
            repositoryExecutionService
                ?? throw new InvalidOperationException("OPENAI_API_KEY is required for repository execution."));
        var threadService = new ThreadConversationService(
            repository,
            processor,
            githubContextService,
            queue,
            processInline: !settings.UsesServiceBus);
        var speechTokenService = new SpeechTokenService(speechHttpClient, settings, secretProvider);

        return new OrchestratorRuntime
        {
            Settings = settings,
            SecretProvider = secretProvider,
            Repository = repository,
            GitHubContextService = githubContextService,
            ThreadService = threadService,
            Processor = processor,
            SpeechTokenService = speechTokenService,
            ProviderAvailability = new ProviderAvailability
            {
                OpenAi = openAiClient is not null,
                Anthropic = anthropicClient is not null,
            },
            Queue = queue,
        };
    }
}
