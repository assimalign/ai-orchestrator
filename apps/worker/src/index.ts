import "dotenv/config";
import { ServiceBusClient } from "@azure/service-bus";
import {
  AnthropicReviewClient,
  createGitHubClient,
  GitHubContextService,
  OpenAiOrchestrationClient,
  OrchestrationEngine,
  RunProcessor,
  SecretProvider,
  TableRunStore,
} from "@ai-dev-orchestrator/orchestrator-core";
import { loadConfig } from "./config";

async function start() {
  const config = loadConfig();
  const secretProvider = new SecretProvider(config.KEY_VAULT_URL);

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

  const runStore = new TableRunStore(
    config.AZURE_STORAGE_CONNECTION_STRING,
    config.RUNS_TABLE_NAME,
  );
  await runStore.init();

  const runProcessor = new RunProcessor({
    store: runStore,
    engine: new OrchestrationEngine({
      githubContextService,
      openAiClient: openAiApiKey
        ? new OpenAiOrchestrationClient(openAiApiKey, config.OPENAI_MODEL)
        : undefined,
      anthropicClient: anthropicApiKey
        ? new AnthropicReviewClient(anthropicApiKey, config.ANTHROPIC_MODEL)
        : undefined,
    }),
  });

  const client = new ServiceBusClient(config.SERVICE_BUS_CONNECTION_STRING);
  const receiver = client.createReceiver(config.SERVICE_BUS_QUEUE_NAME);

  receiver.subscribe({
    processError: async (args) => {
      console.error("Worker subscription error", args.error);
    },
    processMessage: async (message) => {
      const runId = (message.body as { runId?: string })?.runId;

      if (!runId) {
        console.warn("Skipping queue message without runId", message.body);
        return;
      }

      console.log(`Processing run ${runId}`);
      await runProcessor.process(runId);
    },
  });

  console.log("Worker subscribed to Service Bus queue.");
}

start().catch((error) => {
  console.error(error);
  process.exit(1);
});
