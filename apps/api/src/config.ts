import { z } from "zod";

const configSchema = z.object({
  PORT: z.coerce.number().default(8080),
  EXECUTION_MODE: z.enum(["inline", "servicebus"]).default("inline"),
  CORS_ORIGIN: z.string().default("*"),
  RUNS_TABLE_NAME: z.string().default("orchestratorruns"),
  SERVICE_BUS_QUEUE_NAME: z.string().default("orchestrator-runs"),
  OPENAI_MODEL: z.string().default("gpt-5.4"),
  ANTHROPIC_MODEL: z.string().default("claude-sonnet-4-20250514"),
  KEY_VAULT_URL: z.string().optional(),
  AZURE_STORAGE_CONNECTION_STRING: z.string().optional(),
  SERVICE_BUS_CONNECTION_STRING: z.string().optional(),
  AZURE_SPEECH_REGION: z.string().optional(),
  AZURE_SPEECH_KEY: z.string().optional(),
  AZURE_SPEECH_KEY_SECRET_NAME: z.string().default("azure-speech-key"),
  SPEECH_TTS_VOICE: z.string().default("en-US-JennyNeural"),
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

export type ApiConfig = z.infer<typeof configSchema>;

export function loadConfig(): ApiConfig {
  return configSchema.parse(process.env);
}
