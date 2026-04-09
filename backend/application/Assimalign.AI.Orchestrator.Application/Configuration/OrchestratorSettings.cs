using Assimalign.AI.Orchestrator.Core.Models;
using Microsoft.Extensions.Configuration;

namespace Assimalign.AI.Orchestrator.Application.Configuration;

public sealed class OrchestratorSettings
{
    public int Port { get; init; } = 8080;
    public string ExecutionMode { get; init; } = "servicebus";
    public string CorsOrigin { get; init; } = "*";
    public string TableName { get; init; } = "orchestratorstate";
    public string ServiceBusQueueName { get; init; } = "orchestrator-runs";
    public int MaxConsensusRounds { get; init; } = 2;
    public string OpenAiModel { get; init; } = "gpt-5.4";
    public string AnthropicModel { get; init; } = "claude-sonnet-4-20250514";
    public string OpenAiReasoningEffort { get; init; } = "medium";
    public string AnthropicReasoningEffort { get; init; } = "medium";
    public IReadOnlyList<string> OpenAiModelOptions { get; init; } = ["gpt-5.4", "gpt-5.4-mini", "gpt-5-codex"];
    public IReadOnlyList<string> AnthropicModelOptions { get; init; } =
        ["claude-sonnet-4-20250514", "claude-opus-4-1-20250805", "claude-opus-4-20250514", "claude-3-7-sonnet-20250219"];
    public IReadOnlyList<string> OpenAiReasoningEffortOptions { get; init; } = ["low", "medium", "high"];
    public IReadOnlyList<string> AnthropicReasoningEffortOptions { get; init; } = ["none", "low", "medium", "high"];
    public string? KeyVaultUrl { get; init; }
    public string? AzureStorageConnectionString { get; init; }
    public string? ServiceBusConnectionString { get; init; }
    public string? AzureSpeechRegion { get; init; }
    public string SpeechVoice { get; init; } = "en-US-JennyNeural";
    public string? OpenAiApiKey { get; init; }
    public string OpenAiApiKeySecretName { get; init; } = "openai-api-key";
    public string? AnthropicApiKey { get; init; }
    public string AnthropicApiKeySecretName { get; init; } = "anthropic-api-key";
    public string? AzureSpeechKey { get; init; }
    public string AzureSpeechKeySecretName { get; init; } = "azure-speech-key";
    public string? GitHubToken { get; init; }
    public string GitHubTokenSecretName { get; init; } = "github-token";
    public string? GitHubAppId { get; init; }
    public string? GitHubInstallationId { get; init; }
    public string? GitHubAppPrivateKey { get; init; }
    public string GitHubAppPrivateKeySecretName { get; init; } = "github-app-private-key";
    public string RepositoryWorkspaceRoot { get; init; } =
        Path.Combine(Path.GetTempPath(), "assimalign-ai-orchestrator", "workspaces");
    public int ProviderHttpTimeoutSeconds { get; init; } = 900;
    public int RepositoryCommandTimeoutSeconds { get; init; } = 900;
    public int SpeechHttpTimeoutSeconds { get; init; } = 15;
    public string GitCommitUserName { get; init; } = "Assimalign AI Orchestrator";
    public string GitCommitUserEmail { get; init; } = "orchestrator@assimalign.local";
    public string? EntraTenantId { get; init; }
    public string? EntraClientId { get; init; }

    public bool UsesServiceBus =>
        string.Equals(ExecutionMode, "servicebus", StringComparison.OrdinalIgnoreCase);

    public ModelCatalog BuildModelCatalog() =>
        new()
        {
            OpenAi = OpenAiModelOptions.Select(modelId => new ModelOption
            {
                Id = modelId,
                Label = GetModelLabel(modelId),
            }).ToArray(),
            Anthropic = AnthropicModelOptions.Select(modelId => new ModelOption
            {
                Id = modelId,
                Label = GetModelLabel(modelId),
            }).ToArray(),
            OpenAiReasoning = OpenAiReasoningEffortOptions.Select(effort => new ModelOption
            {
                Id = effort,
                Label = GetReasoningLabel(effort),
            }).ToArray(),
            AnthropicReasoning = AnthropicReasoningEffortOptions.Select(effort => new ModelOption
            {
                Id = effort,
                Label = GetReasoningLabel(effort),
            }).ToArray(),
            Defaults = new ModelSelection
            {
                OpenAi = OpenAiModel,
                OpenAiReasoningEffort = OpenAiReasoningEffort,
                Anthropic = AnthropicModel,
                AnthropicReasoningEffort = AnthropicReasoningEffort,
            },
        };

    public static OrchestratorSettings Load(IConfiguration configuration)
    {
        var openAiModel = configuration["OPENAI_MODEL"] ?? "gpt-5.4";
        var anthropicModel = configuration["ANTHROPIC_MODEL"] ?? "claude-sonnet-4-20250514";
        var openAiReasoningEffort = configuration["OPENAI_REASONING_EFFORT"] ?? "medium";
        var anthropicReasoningEffort = configuration["ANTHROPIC_REASONING_EFFORT"] ?? "medium";

        return new OrchestratorSettings
        {
            Port = configuration.GetValue("PORT", 8080),
            ExecutionMode = configuration["EXECUTION_MODE"] ?? "servicebus",
            CorsOrigin = configuration["CORS_ORIGIN"] ?? "*",
            TableName =
                configuration["ORCHESTRATOR_TABLE_NAME"]
                ?? configuration["RUNS_TABLE_NAME"]
                ?? "orchestratorstate",
            ServiceBusQueueName =
                configuration["ORCHESTRATOR_QUEUE_NAME"]
                ?? configuration["SERVICE_BUS_QUEUE_NAME"]
                ?? "orchestrator-runs",
            MaxConsensusRounds = Math.Max(1, configuration.GetValue("MAX_CONSENSUS_ROUNDS", 2)),
            OpenAiModel = openAiModel,
            AnthropicModel = anthropicModel,
            OpenAiReasoningEffort = openAiReasoningEffort,
            AnthropicReasoningEffort = anthropicReasoningEffort,
            OpenAiModelOptions = ParseModelOptions(
                configuration["OPENAI_MODEL_OPTIONS"],
                openAiModel,
                "gpt-5.4-mini",
                "gpt-5-codex"),
            AnthropicModelOptions = ParseModelOptions(
                configuration["ANTHROPIC_MODEL_OPTIONS"],
                anthropicModel,
                "claude-opus-4-1-20250805",
                "claude-opus-4-20250514",
                "claude-3-7-sonnet-20250219"),
            OpenAiReasoningEffortOptions = ParseModelOptions(
                configuration["OPENAI_REASONING_EFFORT_OPTIONS"],
                openAiReasoningEffort,
                "low",
                "medium",
                "high"),
            AnthropicReasoningEffortOptions = ParseModelOptions(
                configuration["ANTHROPIC_REASONING_EFFORT_OPTIONS"],
                anthropicReasoningEffort,
                "none",
                "low",
                "medium",
                "high"),
            KeyVaultUrl = configuration["KEY_VAULT_URL"],
            AzureStorageConnectionString = configuration["AZURE_STORAGE_CONNECTION_STRING"],
            ServiceBusConnectionString = configuration["SERVICE_BUS_CONNECTION_STRING"],
            AzureSpeechRegion = configuration["AZURE_SPEECH_REGION"],
            SpeechVoice =
                configuration["SPEECH_TTS_VOICE"]
                ?? configuration["SPEECH_VOICE"]
                ?? "en-US-JennyNeural",
            OpenAiApiKey = configuration["OPENAI_API_KEY"],
            OpenAiApiKeySecretName = configuration["OPENAI_API_KEY_SECRET_NAME"] ?? "openai-api-key",
            AnthropicApiKey = configuration["ANTHROPIC_API_KEY"],
            AnthropicApiKeySecretName =
                configuration["ANTHROPIC_API_KEY_SECRET_NAME"] ?? "anthropic-api-key",
            AzureSpeechKey = configuration["AZURE_SPEECH_KEY"],
            AzureSpeechKeySecretName =
                configuration["AZURE_SPEECH_KEY_SECRET_NAME"] ?? "azure-speech-key",
            GitHubToken = configuration["GITHUB_TOKEN"],
            GitHubTokenSecretName = configuration["GITHUB_TOKEN_SECRET_NAME"] ?? "github-token",
            GitHubAppId = configuration["GITHUB_APP_ID"],
            GitHubInstallationId = configuration["GITHUB_INSTALLATION_ID"],
            GitHubAppPrivateKey = configuration["GITHUB_APP_PRIVATE_KEY"],
            GitHubAppPrivateKeySecretName =
                configuration["GITHUB_APP_PRIVATE_KEY_SECRET_NAME"] ?? "github-app-private-key",
            RepositoryWorkspaceRoot =
                configuration["REPOSITORY_WORKSPACE_ROOT"]
                ?? Path.Combine(Path.GetTempPath(), "assimalign-ai-orchestrator", "workspaces"),
            ProviderHttpTimeoutSeconds =
                configuration.GetValue("PROVIDER_HTTP_TIMEOUT_SECONDS", 900),
            RepositoryCommandTimeoutSeconds =
                configuration.GetValue("REPOSITORY_COMMAND_TIMEOUT_SECONDS", 900),
            SpeechHttpTimeoutSeconds =
                configuration.GetValue("SPEECH_HTTP_TIMEOUT_SECONDS", 15),
            GitCommitUserName =
                configuration["GIT_COMMIT_USER_NAME"] ?? "Assimalign AI Orchestrator",
            GitCommitUserEmail =
                configuration["GIT_COMMIT_USER_EMAIL"] ?? "orchestrator@assimalign.local",
            EntraTenantId = configuration["ENTRA_TENANT_ID"],
            EntraClientId = configuration["ENTRA_CLIENT_ID"],
        };
    }

    private static IReadOnlyList<string> ParseModelOptions(
        string? configuredValue,
        params string[] fallbackValues)
    {
        var options = (configuredValue ?? string.Empty)
            .Split([',', ';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(fallbackValues)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return options.Length == 0 ? [] : options;
    }

    private static string GetModelLabel(string modelId) =>
        modelId switch
        {
            "gpt-5.4" => "GPT-5.4",
            "gpt-5.4-mini" => "GPT-5.4 mini",
            "gpt-5-codex" => "GPT-5 Codex",
            "claude-sonnet-4-20250514" => "Claude Sonnet 4",
            "claude-opus-4-1-20250805" => "Claude Opus 4.1",
            "claude-opus-4-20250514" => "Claude Opus 4.6",
            "claude-3-7-sonnet-20250219" => "Claude 3.7 Sonnet",
            _ => modelId,
        };

    private static string GetReasoningLabel(string effort) =>
        effort.ToLowerInvariant() switch
        {
            "none" => "Standard",
            "low" => "Low",
            "medium" => "Medium",
            "high" => "High",
            _ => effort,
        };
}
