using Assimalign.AI.Orchestrator.Application.Configuration;
using Assimalign.AI.Orchestrator.Application.Runtime;
using Assimalign.AI.Orchestrator.Application.Services;
using Assimalign.AI.Orchestrator.Core.Models;
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

        var providerHttpClient = new HttpClient();
        var githubContextService = new GitHubContextService(settings, secretProvider);
        var engine = new OrchestrationEngine(
            githubContextService,
            !string.IsNullOrWhiteSpace(openAiApiKey)
                ? new OpenAiOrchestrationClient(providerHttpClient, openAiApiKey, settings.OpenAiModel)
                : null,
            !string.IsNullOrWhiteSpace(anthropicApiKey)
                ? new AnthropicReviewClient(providerHttpClient, anthropicApiKey, settings.AnthropicModel)
                : null);
        var processor = new OrchestrationProcessor(repository, engine, githubContextService);
        var threadService = new ThreadConversationService(
            repository,
            processor,
            githubContextService,
            queue,
            processInline: !settings.UsesServiceBus);
        var speechTokenService = new SpeechTokenService(providerHttpClient, settings, secretProvider);

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
                OpenAi = !string.IsNullOrWhiteSpace(openAiApiKey),
                Anthropic = !string.IsNullOrWhiteSpace(anthropicApiKey),
            },
            Queue = queue,
        };
    }
}
