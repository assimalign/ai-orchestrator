import {
  AnthropicReviewClient,
  createGitHubClient,
  GitHubContextService,
  MemoryRunStore,
  OpenAiOrchestrationClient,
  OrchestrationEngine,
  RunProcessor,
  SecretProvider,
  ServiceBusRunQueue,
  TableRunStore,
} from "@ai-dev-orchestrator/orchestrator-core";
import type { RunQueue, RunStore } from "@ai-dev-orchestrator/orchestrator-core";
import { loadConfig } from "./config";

export interface ApiRuntime {
  config: ReturnType<typeof loadConfig>;
  githubContextService: GitHubContextService;
  runStore: RunStore;
  queue?: RunQueue;
  runProcessor: RunProcessor;
  secretProvider: SecretProvider;
  providerAvailability: {
    openai: boolean;
    anthropic: boolean;
  };
}

export async function createRuntime(): Promise<ApiRuntime> {
  const config = loadConfig();
  const secretProvider = new SecretProvider(config.KEY_VAULT_URL);

  const storageConnectionString = config.AZURE_STORAGE_CONNECTION_STRING;
  const runStore = storageConnectionString
    ? new TableRunStore(storageConnectionString, config.RUNS_TABLE_NAME)
    : new MemoryRunStore();
  await runStore.init();

  const openAiApiKey = await secretProvider.get(
    config.OPENAI_API_KEY_SECRET_NAME,
    config.OPENAI_API_KEY,
  );
  const anthropicApiKey = await secretProvider.get(
    config.ANTHROPIC_API_KEY_SECRET_NAME,
    config.ANTHROPIC_API_KEY,
  );
  const githubToken = await secretProvider.get(
    config.GITHUB_TOKEN_SECRET_NAME,
    config.GITHUB_TOKEN,
  );
  const githubPrivateKey = await secretProvider.get(
    config.GITHUB_APP_PRIVATE_KEY_SECRET_NAME,
    config.GITHUB_APP_PRIVATE_KEY,
  );

  const githubClient = await createGitHubClient({
    token: githubToken,
    appId: config.GITHUB_APP_ID,
    privateKey: githubPrivateKey,
    installationId: config.GITHUB_INSTALLATION_ID,
  });
  const githubContextService = new GitHubContextService(githubClient);

  const engine = new OrchestrationEngine({
    githubContextService,
    openAiClient: openAiApiKey
      ? new OpenAiOrchestrationClient(openAiApiKey, config.OPENAI_MODEL)
      : undefined,
    anthropicClient: anthropicApiKey
      ? new AnthropicReviewClient(anthropicApiKey, config.ANTHROPIC_MODEL)
      : undefined,
  });

  const queue =
    config.EXECUTION_MODE === "servicebus" && config.SERVICE_BUS_CONNECTION_STRING
      ? new ServiceBusRunQueue(
          config.SERVICE_BUS_CONNECTION_STRING,
          config.SERVICE_BUS_QUEUE_NAME,
        )
      : undefined;

  const runProcessor = new RunProcessor({
    store: runStore,
    engine,
  });

  return {
    config,
    githubContextService,
    runStore,
    queue,
    runProcessor,
    secretProvider,
    providerAvailability: {
      openai: Boolean(openAiApiKey),
      anthropic: Boolean(anthropicApiKey),
    },
  };
}
