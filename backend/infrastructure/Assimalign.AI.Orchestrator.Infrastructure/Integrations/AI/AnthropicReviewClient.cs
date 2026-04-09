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

    public async Task<T> GenerateStructuredAsync<T>(
        ProviderPromptRequest promptRequest,
        CancellationToken cancellationToken = default)
    {
        var normalizedReasoningEffort = ResolveReasoningEffort(promptRequest.ReasoningEffort);
        var requestBody = new Dictionary<string, object?>
        {
            ["model"] = ResolveModel(promptRequest.ModelOverride),
            ["max_tokens"] = ResolveMaxTokens(normalizedReasoningEffort),
            ["system"] = promptRequest.SystemPrompt,
            ["messages"] = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "text",
                            text = PromptEnvelopeFormatter.BuildPromptText(promptRequest),
                        },
                    },
                },
            },
        };

        var thinkingBudget = ResolveThinkingBudget(normalizedReasoningEffort);
        if (thinkingBudget > 0)
        {
            requestBody["thinking"] = new
            {
                type = "enabled",
                budget_tokens = thinkingBudget,
            };
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        httpRequest.Headers.Add("x-api-key", apiKey);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpRequest.Content = JsonContent.Create(requestBody, options: JsonDefaults.Options);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
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

        return JsonExtraction.ExtractJsonObject<T>(string.Join(Environment.NewLine, content));
    }

    public async Task<string> GenerateTextAsync(
        ProviderPromptRequest promptRequest,
        CancellationToken cancellationToken = default)
    {
        var normalizedReasoningEffort = ResolveReasoningEffort(promptRequest.ReasoningEffort);
        var requestBody = new Dictionary<string, object?>
        {
            ["model"] = ResolveModel(promptRequest.ModelOverride),
            ["max_tokens"] = ResolveMaxTokens(normalizedReasoningEffort),
            ["system"] = promptRequest.SystemPrompt,
            ["messages"] = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "text",
                            text = PromptEnvelopeFormatter.BuildPromptText(promptRequest),
                        },
                    },
                },
            },
        };

        var thinkingBudget = ResolveThinkingBudget(normalizedReasoningEffort);
        if (thinkingBudget > 0)
        {
            requestBody["thinking"] = new
            {
                type = "enabled",
                budget_tokens = thinkingBudget,
            };
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        httpRequest.Headers.Add("x-api-key", apiKey);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpRequest.Content = JsonContent.Create(requestBody, options: JsonDefaults.Options);

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
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

        return string.Join(Environment.NewLine, content);
    }

    private string ResolveModel(string? modelOverride) =>
        string.IsNullOrWhiteSpace(modelOverride) ? model : modelOverride.Trim();

    private static string ResolveReasoningEffort(string? reasoningEffortOverride) =>
        string.IsNullOrWhiteSpace(reasoningEffortOverride)
            ? "medium"
            : reasoningEffortOverride.Trim().ToLowerInvariant();

    private static int ResolveThinkingBudget(string reasoningEffort) =>
        reasoningEffort switch
        {
            "none" => 0,
            "low" => 1024,
            "medium" => 2048,
            "high" => 4096,
            _ => 2048,
        };

    private static int ResolveMaxTokens(string reasoningEffort)
    {
        var thinkingBudget = ResolveThinkingBudget(reasoningEffort);
        return thinkingBudget > 0 ? thinkingBudget + 1600 : 1600;
    }

}
