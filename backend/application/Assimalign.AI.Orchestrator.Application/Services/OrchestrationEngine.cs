using Assimalign.AI.Orchestrator.Application.Abstractions.GitHub;
using Assimalign.AI.Orchestrator.Application.Abstractions.Providers;
using Assimalign.AI.Orchestrator.Core.Models;
using Assimalign.AI.Orchestrator.Core.Prompts;

namespace Assimalign.AI.Orchestrator.Application.Services;

public sealed class OrchestrationEngine(
    int maxConsensusRounds,
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

        var plan = await openAiClient.GenerateStructuredAsync<PlanningArtifact>(
            BuildPlanRequest(input, context, threadHistory),
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
        DebateArtifact? debate = null;
        if (anthropicClient is not null)
        {
            try
            {
                review = new ReviewArtifact();

                for (var round = 0; round < Math.Max(1, maxConsensusRounds); round++)
                {
                    if (onStage is not null)
                    {
                        await onStage(new StageUpdate { Status = ThreadStageStatus.Reviewing });
                    }

                    review = await anthropicClient.GenerateStructuredAsync<ReviewArtifact>(
                        BuildReviewRequest(input, context, threadHistory, plan, debate),
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

                    if (review.NeedsUserDecision)
                    {
                        return BuildUserDecisionResult(context, plan, review, debate);
                    }

                    if (onStage is not null)
                    {
                        await onStage(new StageUpdate { Status = ThreadStageStatus.Synthesizing });
                    }

                    debate = await openAiClient.GenerateStructuredAsync<DebateArtifact>(
                        BuildDebateRequest(input, context, threadHistory, plan, review, debate),
                        cancellationToken);

                    if (onStage is not null)
                    {
                        await onStage(new StageUpdate
                        {
                            Status = ThreadStageStatus.Synthesizing,
                            Message = new ThreadMessage
                            {
                                Id = Guid.NewGuid().ToString("D"),
                                Role = ThreadMessageRole.Stage,
                                Stage = ThreadStageStatus.Synthesizing,
                                Title = BuildStageTitle("Codex", input.Models?.OpenAi ?? openAiClient.DefaultModel),
                                Content = debate.Message.Trim(),
                                Provider = "codex",
                                CreatedAt = DateTimeOffset.UtcNow,
                                Metadata = BuildModelMetadata(input.Models?.OpenAi ?? openAiClient.DefaultModel),
                            },
                        });
                    }

                    if (debate.NeedsUserDecision)
                    {
                        return BuildUserDecisionResult(context, plan, review, debate);
                    }

                    if (review.IsAligned || debate.IsAligned)
                    {
                        break;
                    }
                }

                if (debate is not null && !debate.IsAligned && !review.IsAligned)
                {
                    return BuildFallbackUserDecisionResult(context, plan, review, debate);
                }
            }
            catch (Exception error)
            {
                review = new ReviewArtifact
                {
                    Message = BuildReviewFallbackMessage(error),
                };

                if (onStage is not null)
                {
                    await onStage(new StageUpdate
                    {
                        Status = ThreadStageStatus.Reviewing,
                        Message = new ThreadMessage
                        {
                            Id = Guid.NewGuid().ToString("D"),
                            Role = ThreadMessageRole.Stage,
                            Stage = ThreadStageStatus.Failed,
                            Title = BuildStageTitle("Claude unavailable", input.Models?.Anthropic ?? anthropicClient.DefaultModel),
                            Content = review.Message,
                            Provider = "claude",
                            CreatedAt = DateTimeOffset.UtcNow,
                            Metadata = BuildModelMetadata(
                                input.Models?.Anthropic ?? anthropicClient.DefaultModel),
                        },
                    });
                }
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

        var summary = await openAiClient.GenerateTextAsync(
            BuildSummaryRequest(input, context, threadHistory, plan, review, debate),
            cancellationToken);

        return new OrchestrationResult
        {
            Context = context,
            Plan = plan,
            Review = review,
            Debate = debate,
            Summary = summary.Trim(),
        };
    }

    private static string BuildStageTitle(string baseTitle, string modelId) =>
        string.IsNullOrWhiteSpace(modelId) ? baseTitle : $"{baseTitle} · {modelId}";

    private static ProviderPromptRequest BuildPlanRequest(
        ConversationInput input,
        GitHubContextSnapshot? context,
        IReadOnlyList<ThreadMessage>? threadHistory) =>
        new()
        {
            SystemPrompt = PromptLibrary.PlannerSystemPrompt,
            Requirement = input.Text,
            Context = context,
            ThreadHistory = threadHistory,
            ModelOverride = input.Models?.OpenAi,
            ReasoningEffort = input.Models?.OpenAiReasoningEffort ?? "medium",
        };

    private static ProviderPromptRequest BuildReviewRequest(
        ConversationInput input,
        GitHubContextSnapshot? context,
        IReadOnlyList<ThreadMessage>? threadHistory,
        PlanningArtifact plan,
        DebateArtifact? debate) =>
        new()
        {
            SystemPrompt = PromptLibrary.ReviewerSystemPrompt,
            Requirement = input.Text,
            Context = context,
            ThreadHistory = threadHistory,
            Plan = plan,
            Debate = debate,
            ModelOverride = input.Models?.Anthropic,
            ReasoningEffort = input.Models?.AnthropicReasoningEffort ?? "medium",
        };

    private static ProviderPromptRequest BuildDebateRequest(
        ConversationInput input,
        GitHubContextSnapshot? context,
        IReadOnlyList<ThreadMessage>? threadHistory,
        PlanningArtifact plan,
        ReviewArtifact review,
        DebateArtifact? debate) =>
        new()
        {
            SystemPrompt = PromptLibrary.DebateSystemPrompt,
            Requirement = input.Text,
            Context = context,
            ThreadHistory = threadHistory,
            Plan = plan,
            Review = review,
            Debate = debate,
            ModelOverride = input.Models?.OpenAi,
            ReasoningEffort = input.Models?.OpenAiReasoningEffort ?? "medium",
        };

    private static ProviderPromptRequest BuildSummaryRequest(
        ConversationInput input,
        GitHubContextSnapshot? context,
        IReadOnlyList<ThreadMessage>? threadHistory,
        PlanningArtifact plan,
        ReviewArtifact review,
        DebateArtifact? debate) =>
        new()
        {
            SystemPrompt = PromptLibrary.SynthesizerSystemPrompt,
            Requirement = input.Text,
            Context = context,
            ThreadHistory = threadHistory,
            Plan = plan,
            Review = review,
            Debate = debate,
            ModelOverride = input.Models?.OpenAi,
            ReasoningEffort = input.Models?.OpenAiReasoningEffort ?? "low",
        };

    private static OrchestrationResult BuildUserDecisionResult(
        GitHubContextSnapshot? context,
        PlanningArtifact plan,
        ReviewArtifact review,
        DebateArtifact? debate)
    {
        var prompt = !string.IsNullOrWhiteSpace(debate?.UserDecisionPrompt)
            ? debate.UserDecisionPrompt
            : !string.IsNullOrWhiteSpace(review.UserDecisionPrompt)
                ? review.UserDecisionPrompt
                : "Codex and Claude surfaced a tradeoff that needs your call before implementation continues.";

        return new OrchestrationResult
        {
            Context = context,
            Plan = plan,
            Review = review,
            Debate = debate,
            Summary = prompt.Trim(),
            NeedsUserDecision = true,
        };
    }

    private static OrchestrationResult BuildFallbackUserDecisionResult(
        GitHubContextSnapshot? context,
        PlanningArtifact plan,
        ReviewArtifact review,
        DebateArtifact debate)
    {
        var prompt = !string.IsNullOrWhiteSpace(debate.UserDecisionPrompt)
            ? debate.UserDecisionPrompt
            : !string.IsNullOrWhiteSpace(review.UserDecisionPrompt)
                ? review.UserDecisionPrompt
                : "Codex and Claude still disagree after the allotted debate rounds. Which direction should we take?";

        return new OrchestrationResult
        {
            Context = context,
            Plan = plan,
            Review = review,
            Debate = debate,
            Summary = prompt.Trim(),
            NeedsUserDecision = true,
        };
    }

    private static string BuildReviewFallbackMessage(Exception error)
    {
        var message = error.GetBaseException().Message;

        if (message.Contains("credit balance is too low", StringComparison.OrdinalIgnoreCase))
        {
            return "Claude review was skipped because the Anthropic API credit balance is too low for this run. The conversation continued with Codex only.";
        }

        return $"Claude review was skipped for this run. The conversation continued with Codex only. Details: {message}";
    }

    private static Dictionary<string, string>? BuildModelMetadata(string? modelId) =>
        string.IsNullOrWhiteSpace(modelId)
            ? null
            : new Dictionary<string, string>
            {
                ["model"] = modelId,
            };
}
