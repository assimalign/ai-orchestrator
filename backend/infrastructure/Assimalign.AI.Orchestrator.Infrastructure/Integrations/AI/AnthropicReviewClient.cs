using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Assimalign.AI.Orchestrator.Application.Abstractions.Providers;
using Assimalign.AI.Orchestrator.Core.Models;
using Assimalign.AI.Orchestrator.Core.Prompts;
using Assimalign.AI.Orchestrator.Core.Utilities;

namespace Assimalign.AI.Orchestrator.Infrastructure.Integrations.AI;

public sealed class AnthropicReviewClient(HttpClient httpClient, string apiKey, string model)
    : IAnthropicReviewClient
{
    public string DefaultModel => model;

    public async Task<ReviewArtifact> CritiquePlanAsync(
        string requirement,
        PlanningArtifact plan,
        GitHubContextSnapshot? context,
        IReadOnlyList<ThreadMessage>? threadHistory,
        string? modelOverride = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = JsonContent.Create(
            new
            {
                model = ResolveModel(modelOverride),
                max_tokens = 1200,
                system = PromptLibrary.ReviewerSystemPrompt,
                messages = new object[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "text",
                                text = string.Join(
                                    Environment.NewLine,
                                    new[]
                                    {
                                        "Requirement:",
                                        requirement,
                                        string.Empty,
                                        "GitHub context:",
                                        JsonExtraction.Serialize((object?)context ?? new Dictionary<string, string>()),
                                        string.Empty,
                                        "Thread history:",
                                        FormatThreadHistory(threadHistory),
                                        string.Empty,
                                        "Codex draft:",
                                        plan.Message,
                                        string.IsNullOrWhiteSpace(plan.SuggestedBranchName)
                                            ? string.Empty
                                            : $"Suggested branch name: {plan.SuggestedBranchName}",
                                    }),
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
                $"Anthropic request failed with {(int)response.StatusCode}: {body}");
        }

        using var document = JsonDocument.Parse(body);
        var content = document.RootElement.GetProperty("content")
            .EnumerateArray()
            .Where(item => item.GetProperty("type").GetString() == "text")
            .Select(item => item.GetProperty("text").GetString() ?? string.Empty);

        return JsonExtraction.ExtractJsonObject<ReviewArtifact>(string.Join(Environment.NewLine, content));
    }

    private string ResolveModel(string? modelOverride) =>
        string.IsNullOrWhiteSpace(modelOverride) ? model : modelOverride.Trim();

    private static string FormatThreadHistory(IReadOnlyList<ThreadMessage>? threadHistory)
    {
        if (threadHistory is not { Count: > 0 })
        {
            return "No prior thread history.";
        }

        return string.Join(
            Environment.NewLine,
            threadHistory
                .Where(message => message.Role is not ThreadMessageRole.System)
                .TakeLast(8)
                .Select(
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
}
