using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Assimalign.AI.Orchestrator.Application.Abstractions.Providers;
using Assimalign.AI.Orchestrator.Core.Models;
using Assimalign.AI.Orchestrator.Core.Prompts;
using Assimalign.AI.Orchestrator.Core.Utilities;

namespace Assimalign.AI.Orchestrator.Infrastructure.Integrations.AI;

public sealed class OpenAiOrchestrationClient(HttpClient httpClient, string apiKey, string model)
    : IOpenAiOrchestrationClient
{
    public string DefaultModel => model;

    public async Task<T> GenerateStructuredAsync<T>(
        ProviderPromptRequest request,
        CancellationToken cancellationToken = default)
    {
        var responseText = await SendAsync(
            request,
            cancellationToken);

        return JsonExtraction.ExtractJsonObject<T>(responseText);
    }

    public Task<string> GenerateTextAsync(
        ProviderPromptRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(request, cancellationToken);
    }

    private async Task<string> SendAsync(
        ProviderPromptRequest promptRequest,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Content = JsonContent.Create(
            new
            {
                model = ResolveModel(promptRequest.ModelOverride),
                instructions = promptRequest.SystemPrompt,
                reasoning = new { effort = ResolveReasoningEffort(promptRequest.ReasoningEffort, "medium") },
                input = new object[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "input_text",
                                text = PromptEnvelopeFormatter.BuildPromptText(promptRequest),
                            },
                        },
                    },
                },
            },
            options: JsonDefaults.Options);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"OpenAI request failed with {(int)response.StatusCode}: {body}");
        }

        using var document = JsonDocument.Parse(body);
        return ExtractOutputText(document.RootElement);
    }

    private static string BuildExecutionContextText(
        string repositoryTree,
        string executionEnvironment)
    {
        return string.Join(
            Environment.NewLine,
            [
                "Execution environment:",
                executionEnvironment,
                string.Empty,
                "Repository tree:",
                repositoryTree,
            ]);
    }

    private static string BuildInspectionSummaryText(
        RepositoryInspectionContextArtifact inspectionContext,
        string repositoryTree,
        IReadOnlyDictionary<string, string> fileContents)
    {
        var lines = new List<string>
        {
            "Inspection context:",
            JsonExtraction.Serialize(inspectionContext),
            string.Empty,
            "Repository tree:",
            repositoryTree,
            string.Empty,
            "Selected file contents:",
        };

        if (fileContents.Count == 0)
        {
            lines.Add("No file contents were provided.");
            return string.Join(Environment.NewLine, lines);
        }

        foreach (var entry in fileContents)
        {
            lines.Add($"--- FILE: {entry.Key} ---");
            lines.Add(entry.Value);
            lines.Add($"--- END FILE: {entry.Key} ---");
            lines.Add(string.Empty);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildExecutionArtifactText(
        RepositoryExecutionContextArtifact executionContext,
        string repositoryTree,
        string executionEnvironment,
        IReadOnlyDictionary<string, string> fileContents)
    {
        var lines = new List<string>
        {
            "Execution environment:",
            executionEnvironment,
            string.Empty,
            "Execution context:",
            JsonExtraction.Serialize(executionContext),
            string.Empty,
            "Repository tree:",
            repositoryTree,
            string.Empty,
            "Selected file contents:",
        };

        if (fileContents.Count == 0)
        {
            lines.Add("No file contents were provided.");
            return string.Join(Environment.NewLine, lines);
        }

        foreach (var entry in fileContents)
        {
            lines.Add($"--- FILE: {entry.Key} ---");
            lines.Add(entry.Value);
            lines.Add($"--- END FILE: {entry.Key} ---");
            lines.Add(string.Empty);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string ExtractOutputText(JsonElement root)
    {
        if (root.TryGetProperty("output_text", out var outputText))
        {
            return outputText.GetString() ?? string.Empty;
        }

        if (root.TryGetProperty("output", out var outputArray))
        {
            var parts = new List<string>();

            foreach (var item in outputArray.EnumerateArray())
            {
                if (!item.TryGetProperty("content", out var contentArray))
                {
                    continue;
                }

                foreach (var contentItem in contentArray.EnumerateArray())
                {
                    if (contentItem.TryGetProperty("text", out var text))
                    {
                        parts.Add(text.GetString() ?? string.Empty);
                    }
                }
            }

            return string.Join(Environment.NewLine, parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }

        return string.Empty;
    }

    private string ResolveModel(string? modelOverride) =>
        string.IsNullOrWhiteSpace(modelOverride) ? model : modelOverride.Trim();

    private static string ResolveReasoningEffort(
        string? reasoningEffortOverride,
        string fallback) =>
        string.IsNullOrWhiteSpace(reasoningEffortOverride)
            ? fallback
            : reasoningEffortOverride.Trim().ToLowerInvariant();
}
