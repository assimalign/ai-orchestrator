import { z } from "zod";

const configSchema = z.object({
  SERVICE_BUS_CONNECTION_STRING: z.string(),
  SERVICE_BUS_QUEUE_NAME: z.string().default("orchestrator-runs"),
  RUNS_TABLE_NAME: z.string().default("orchestratorruns"),
  AZURE_STORAGE_CONNECTION_STRING: z.string(),
  KEY_VAULT_URL: z.string().optional(),
  OPENAI_MODEL: z.string().default("gpt-5.4"),
  ANTHROPIC_MODEL: z.string().default("claude-sonnet-4-20250514"),
  OPENAI_API_KEY: z.string().optional(),
  OPENAI_API_KEY_SECRET_NAME: z.string().default("openai-api-key"),
  ANTHROPIC_API_KEY: z.string().optional(),
  ANTHROPIC_API_KEY_SECRET_NAME: z.string().default("anthropic-api-key"),
  GITHUB_TOKEN: z.string().optional(),
  GITHUB_TOKEN_SECRET_NAME: z.string().default("github-token"),
  GITHUB_APP_ID: z.string().optional(),
  GITHUB_INSTALLATION_ID: z.string().optional(),
  GITHUB_APP_PRIVATE_KEY: z.string().optional(),
  GITHUB_APP_PRIVATE_KEY_SECRET_NAME: z.string().default("github-app-private-key"),
});

export type WorkerConfig = z.infer<typeof configSchema>;

export function loadConfig(): WorkerConfig {
  return configSchema.parse(process.env);
}
