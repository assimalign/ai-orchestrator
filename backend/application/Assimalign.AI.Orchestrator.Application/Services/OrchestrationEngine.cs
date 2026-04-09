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
                    Title = BuildStageTitle("Codex", input.Models?.OpenAi ?? openAiClient.DefaultModel),
                    Content = plan.Message.Trim(),
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
                        Title = BuildStageTitle("Claude", input.Models?.Anthropic ?? anthropicClient.DefaultModel),
                        Content = review.Message.Trim(),
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
