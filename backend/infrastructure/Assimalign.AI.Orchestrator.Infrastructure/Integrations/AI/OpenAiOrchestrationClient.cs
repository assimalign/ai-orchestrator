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

    public async Task<PlanningArtifact> CreatePlanAsync(
        string requirement,
        GitHubContextSnapshot? context,
        IReadOnlyList<ThreadMessage>? threadHistory,
        string? modelOverride = null,
        CancellationToken cancellationToken = default)
    {
        var responseText = await SendAsync(
            PromptLibrary.PlannerSystemPrompt,
            requirement,
            context,
            threadHistory,
            null,
            null,
            null,
            "medium",
            modelOverride,
            cancellationToken);

        return JsonExtraction.ExtractJsonObject<PlanningArtifact>(responseText);
    }

    public Task<string> SynthesizeBriefAsync(
        string requirement,
        PlanningArtifact plan,
        ReviewArtifact review,
        string? codexDebateReply,
        GitHubContextSnapshot? context,
        IReadOnlyList<ThreadMessage>? threadHistory,
        string? modelOverride = null,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(
            PromptLibrary.SynthesizerSystemPrompt,
            requirement,
            context,
            threadHistory,
            plan,
            review,
            codexDebateReply,
            "low",
            modelOverride,
            cancellationToken);
    }

    public Task<string> RespondToReviewAsync(
        string requirement,
        PlanningArtifact plan,
        ReviewArtifact review,
        GitHubContextSnapshot? context,
        IReadOnlyList<ThreadMessage>? threadHistory,
        string? modelOverride = null,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(
            PromptLibrary.DebateSystemPrompt,
            requirement,
            context,
            threadHistory,
            plan,
            review,
            null,
            "medium",
            modelOverride,
            cancellationToken);
    }

    private async Task<string> SendAsync(
        string instructions,
        string requirement,
        GitHubContextSnapshot? context,
        IReadOnlyList<ThreadMessage>? threadHistory,
        PlanningArtifact? plan,
        ReviewArtifact? review,
        string? codexDebateReply,
        string reasoningEffort,
        string? modelOverride,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(
            new
            {
                model = ResolveModel(modelOverride),
                instructions,
                reasoning = new { effort = reasoningEffort },
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
                                text = BuildPromptText(requirement, context, threadHistory, plan, review, codexDebateReply),
                            },
                        },
                    },
                },
            },
            options: JsonDefaults.Options);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"OpenAI request failed with {(int)response.StatusCode}: {body}");
        }

        using var document = JsonDocument.Parse(body);
        return ExtractOutputText(document.RootElement);
    }

    private static string BuildPromptText(
        string requirement,
        GitHubContextSnapshot? context,
        IReadOnlyList<ThreadMessage>? threadHistory,
        PlanningArtifact? plan,
        ReviewArtifact? review,
        string? codexDebateReply)
    {
        var lines = new List<string>
        {
            "Latest user request:",
            requirement,
            string.Empty,
            "GitHub context:",
            JsonExtraction.Serialize((object?)context ?? new Dictionary<string, string>()),
        };

        if (threadHistory is { Count: > 0 })
        {
            lines.Add(string.Empty);
            lines.Add("Thread history:");
            lines.Add(FormatThreadHistory(threadHistory));
        }

        if (plan is not null)
        {
            lines.Add(string.Empty);
            lines.Add("Codex draft:");
            lines.Add(plan.Message);

            lines.Add(string.Empty);
            lines.Add($"Requires implementation: {(plan.RequiresImplementation ? "yes" : "no")}");

            if (!string.IsNullOrWhiteSpace(plan.SuggestedBranchName))
            {
                lines.Add(string.Empty);
                lines.Add($"Suggested branch name: {plan.SuggestedBranchName}");
            }
        }

        if (review is not null)
        {
            lines.Add(string.Empty);
            lines.Add("Claude feedback:");
            lines.Add(review.Message);
        }

        if (!string.IsNullOrWhiteSpace(codexDebateReply))
        {
            lines.Add(string.Empty);
            lines.Add("Codex response to Claude:");
            lines.Add(codexDebateReply);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatThreadHistory(IReadOnlyList<ThreadMessage> threadHistory)
    {
        var relevantMessages = threadHistory
            .Where(message => message.Role is not ThreadMessageRole.System)
            .TakeLast(8)
            .ToArray();

        if (relevantMessages.Length == 0)
        {
            return "No prior thread history.";
        }

        return string.Join(
            Environment.NewLine,
            relevantMessages.Select(
                message =>
                    $"[{message.CreatedAt:HH:mm}] {BuildMessageLabel(message)}: {message.Content.Trim()}"));
    }

    private static string BuildMessageLabel(ThreadMessage message)
    {
        return message.Role switch
        {
            ThreadMessageRole.User => "User",
            ThreadMessageRole.Assistant => "Codex",
            ThreadMessageRole.Stage => $"{BuildProviderLabel(message.Provider)} {message.Stage?.ToString() ?? "update"}",
            _ => "System",
        };
    }

    private static string BuildProviderLabel(string? provider) =>
        provider?.ToLowerInvariant() switch
        {
            "claude" => "Claude",
            "codex" => "Codex",
            _ => "Agent",
        };

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
}
