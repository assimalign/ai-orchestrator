using Assimalign.AI.Orchestrator.Application.Abstractions.GitHub;
using Assimalign.AI.Orchestrator.Application.Abstractions.Providers;
using Assimalign.AI.Orchestrator.Core.Models;

namespace Assimalign.AI.Orchestrator.Application.Services;

public sealed class OrchestrationEngine(
    IGitHubContextService gitHubContextService,
    IOpenAiOrchestrationClient? openAiClient,
    IAnthropicReviewClient? anthropicClient)
{
    public async Task<OrchestrationResult> ExecuteAsync(
        ConversationInput input,
        IReadOnlyList<ThreadMessage>? threadHistory = null,
        Func<StageUpdate, Task>? onStage = null,
        CancellationToken cancellationToken = default)
    {
        if (openAiClient is null)
        {
            throw new InvalidOperationException("OPENAI_API_KEY is required to generate a Codex plan.");
        }

        var context = await gitHubContextService.BuildSnapshotAsync(input.Repository, cancellationToken);

        if (onStage is not null)
        {
            await onStage(new StageUpdate { Status = ThreadStageStatus.Planning });
        }

        var plan = await openAiClient.CreatePlanAsync(
            input.Text,
            context,
            threadHistory,
            input.Models?.OpenAi,
            cancellationToken);
        if (onStage is not null)
        {
            await onStage(new StageUpdate
            {
                Status = ThreadStageStatus.Planning,
                Message = new ThreadMessage
                {
                    Id = Guid.NewGuid().ToString("D"),
                    Role = ThreadMessageRole.Stage,
                    Stage = ThreadStageStatus.Planning,
                    Title = BuildStageTitle("Codex drafted the plan", input.Models?.OpenAi ?? openAiClient.DefaultModel),
                    Content = FormatPlanningArtifact(plan),
                    Provider = "codex",
                    CreatedAt = DateTimeOffset.UtcNow,
                    Metadata = BuildModelMetadata(input.Models?.OpenAi ?? openAiClient.DefaultModel),
                },
            });
        }

        ReviewArtifact review;
        if (anthropicClient is not null)
        {
            if (onStage is not null)
            {
                await onStage(new StageUpdate { Status = ThreadStageStatus.Reviewing });
            }

            review = await anthropicClient.CritiquePlanAsync(
                input.Text,
                plan,
                context,
                threadHistory,
                input.Models?.Anthropic,
                cancellationToken);
            if (onStage is not null)
            {
                await onStage(new StageUpdate
                {
                    Status = ThreadStageStatus.Reviewing,
                    Message = new ThreadMessage
                    {
                        Id = Guid.NewGuid().ToString("D"),
                        Role = ThreadMessageRole.Stage,
                        Stage = ThreadStageStatus.Reviewing,
                        Title = BuildStageTitle(
                            "Claude reviewed the plan",
                            input.Models?.Anthropic ?? anthropicClient.DefaultModel),
                        Content = FormatReviewArtifact(review),
                        Provider = "claude",
                        CreatedAt = DateTimeOffset.UtcNow,
                        Metadata = BuildModelMetadata(
                            input.Models?.Anthropic ?? anthropicClient.DefaultModel),
                    },
                });
            }
        }
        else
        {
            review = new ReviewArtifact();
        }

        if (onStage is not null)
        {
            await onStage(new StageUpdate { Status = ThreadStageStatus.Synthesizing });
        }

        var summary = await openAiClient.SynthesizeBriefAsync(
            input.Text,
            plan,
            review,
            context,
            threadHistory,
            input.Models?.OpenAi,
            cancellationToken);

        return new OrchestrationResult
        {
            Context = context,
            Plan = plan,
            Review = review,
            Summary = summary.Trim(),
        };
    }

    private static string FormatPlanningArtifact(PlanningArtifact plan)
    {
        var lines = new List<string>
        {
            $"Objective: {plan.Objective}",
            string.Empty,
            "First tasks:",
        };

        lines.AddRange(plan.FirstTasks.Take(3).Select(task => $"- {task}"));
        lines.Add(string.Empty);
        lines.Add("Key risks:");
        lines.AddRange(plan.Risks.Take(2).Select(risk => $"- {risk}"));

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatReviewArtifact(ReviewArtifact review)
    {
        var lines = new List<string>
        {
            "Watchouts:",
        };

        lines.AddRange(review.Concerns.Take(2).Select(item => $"- {item}"));
        lines.Add(string.Empty);
        lines.Add("Adjustments:");
        lines.AddRange(review.Improvements.Take(2).Select(item => $"- {item}"));

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildStageTitle(string baseTitle, string modelId) =>
        string.IsNullOrWhiteSpace(modelId) ? baseTitle : $"{baseTitle} · {modelId}";

    private static Dictionary<string, string>? BuildModelMetadata(string? modelId) =>
        string.IsNullOrWhiteSpace(modelId)
            ? null
            : new Dictionary<string, string>
            {
                ["model"] = modelId,
            };
}
