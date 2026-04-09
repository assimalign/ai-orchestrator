using Assimalign.AI.Orchestrator.Application.Abstractions.GitHub;
using Assimalign.AI.Orchestrator.Application.Abstractions.Providers;
using Assimalign.AI.Orchestrator.Core.Models;
using Assimalign.AI.Orchestrator.Core.Prompts;
using Assimalign.AI.Orchestrator.Core.Utilities;

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

        var codexOpeningTask = openAiClient.GenerateStructuredAsync<PlanningArtifact>(
            BuildCodexOpeningRequest(input, context, threadHistory),
            cancellationToken);
        var claudeOpeningTask = anthropicClient?.GenerateStructuredAsync<PlanningArtifact>(
            BuildClaudeOpeningRequest(input, context, threadHistory),
            cancellationToken);

        var codexOpening = await codexOpeningTask;
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
                    Title = BuildStageTitle("Codex opening", input.Models?.OpenAi ?? openAiClient.DefaultModel),
                    Content = BuildStageContent(codexOpening.Message, codexOpening.Reasoning),
                    Provider = "codex",
                    CreatedAt = DateTimeOffset.UtcNow,
                    Metadata = BuildModelMetadata(input.Models?.OpenAi ?? openAiClient.DefaultModel),
                },
            });
        }

        PlanningArtifact? claudeOpening = null;
        ReviewArtifact review;
        DebateArtifact? debate = null;
        if (anthropicClient is not null && claudeOpeningTask is not null)
        {
            try
            {
                claudeOpening = await claudeOpeningTask;

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
                            Title = BuildStageTitle("Claude opening", input.Models?.Anthropic ?? anthropicClient.DefaultModel),
                            Content = BuildStageContent(claudeOpening.Message, claudeOpening.Reasoning),
                            Provider = "claude",
                            CreatedAt = DateTimeOffset.UtcNow,
                            Metadata = BuildModelMetadata(
                                input.Models?.Anthropic ?? anthropicClient.DefaultModel),
                        },
                    });
                }

                review = new ReviewArtifact();

                for (var round = 0; round < Math.Max(1, maxConsensusRounds); round++)
                {
                    if (onStage is not null)
                    {
                        await onStage(new StageUpdate { Status = ThreadStageStatus.Reviewing });
                    }

                    var codexComparisonTask = openAiClient.GenerateStructuredAsync<DebateArtifact>(
                        BuildCodexComparisonRequest(input, context, threadHistory, codexOpening, claudeOpening, review, debate, round),
                        cancellationToken);
                    var claudeComparisonTask = anthropicClient.GenerateStructuredAsync<ReviewArtifact>(
                        BuildClaudeComparisonRequest(input, context, threadHistory, codexOpening, claudeOpening, review, debate, round),
                        cancellationToken);

                    debate = await codexComparisonTask;
                    review = await claudeComparisonTask;

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
                                Title = BuildStageTitle("Codex comparison", input.Models?.OpenAi ?? openAiClient.DefaultModel),
                                Content = BuildStageContent(debate.Message, debate.Reasoning),
                                Provider = "codex",
                                CreatedAt = DateTimeOffset.UtcNow,
                                Metadata = BuildModelMetadata(input.Models?.OpenAi ?? openAiClient.DefaultModel),
                            },
                        });
                        await onStage(new StageUpdate
                        {
                            Status = ThreadStageStatus.Reviewing,
                            Message = new ThreadMessage
                            {
                                Id = Guid.NewGuid().ToString("D"),
                                Role = ThreadMessageRole.Stage,
                                Stage = ThreadStageStatus.Reviewing,
                                Title = BuildStageTitle("Claude comparison", input.Models?.Anthropic ?? anthropicClient.DefaultModel),
                                Content = BuildStageContent(review.Message, review.Reasoning),
                                Provider = "claude",
                                CreatedAt = DateTimeOffset.UtcNow,
                                Metadata = BuildModelMetadata(
                                    input.Models?.Anthropic ?? anthropicClient.DefaultModel),
                            },
                        });
                    }

                    if (review.NeedsUserDecision || debate.NeedsUserDecision)
                    {
                        return BuildUserDecisionResult(context, codexOpening, claudeOpening, review, debate);
                    }

                    if (HasConsensus(review, debate))
                    {
                        break;
                    }
                }

                if (debate is not null && !HasConsensus(review, debate))
                {
                    return BuildFallbackUserDecisionResult(context, codexOpening, claudeOpening, review, debate);
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

        var plan = claudeOpening is not null
            ? await openAiClient.GenerateStructuredAsync<PlanningArtifact>(
                BuildAgreementPlanRequest(input, context, threadHistory, codexOpening, claudeOpening, review, debate),
                cancellationToken)
            : codexOpening;

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
                    Title = BuildStageTitle("Codex agreement", input.Models?.OpenAi ?? openAiClient.DefaultModel),
                    Content = BuildStageContent(plan.Message, plan.Reasoning),
                    Provider = "codex",
                    CreatedAt = DateTimeOffset.UtcNow,
                    Metadata = BuildModelMetadata(input.Models?.OpenAi ?? openAiClient.DefaultModel),
                },
            });
        }

        var summary = await openAiClient.GenerateTextAsync(
            BuildSummaryRequest(input, context, threadHistory, codexOpening, claudeOpening, plan, review, debate),
            cancellationToken);

        return new OrchestrationResult
        {
            Context = context,
            CodexOpening = codexOpening,
            ClaudeOpening = claudeOpening,
            Plan = plan,
            Review = review,
            Debate = debate,
            Summary = summary.Trim(),
        };
    }

    private static string BuildStageTitle(string baseTitle, string modelId) =>
        string.IsNullOrWhiteSpace(modelId) ? baseTitle : $"{baseTitle} · {modelId}";

    private static ProviderPromptRequest BuildCodexOpeningRequest(
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

    private static ProviderPromptRequest BuildClaudeOpeningRequest(
        ConversationInput input,
        GitHubContextSnapshot? context,
        IReadOnlyList<ThreadMessage>? threadHistory) =>
        new()
        {
            SystemPrompt = PromptLibrary.ClaudeOpeningSystemPrompt,
            Requirement = input.Text,
            Context = context,
            ThreadHistory = threadHistory,
            ModelOverride = input.Models?.Anthropic,
            ReasoningEffort = input.Models?.AnthropicReasoningEffort ?? "medium",
        };

    private static ProviderPromptRequest BuildClaudeComparisonRequest(
        ConversationInput input,
        GitHubContextSnapshot? context,
        IReadOnlyList<ThreadMessage>? threadHistory,
        PlanningArtifact codexOpening,
        PlanningArtifact claudeOpening,
        ReviewArtifact? review,
        DebateArtifact? debate,
        int round) =>
        new()
        {
            SystemPrompt = PromptLibrary.ReviewerSystemPrompt,
            Requirement = input.Text,
            Context = context,
            ThreadHistory = threadHistory,
            AdditionalContext = BuildComparisonContext(codexOpening, claudeOpening, review, debate, round),
            ModelOverride = input.Models?.Anthropic,
            ReasoningEffort = input.Models?.AnthropicReasoningEffort ?? "medium",
        };

    private static ProviderPromptRequest BuildCodexComparisonRequest(
        ConversationInput input,
        GitHubContextSnapshot? context,
        IReadOnlyList<ThreadMessage>? threadHistory,
        PlanningArtifact codexOpening,
        PlanningArtifact claudeOpening,
        ReviewArtifact review,
        DebateArtifact? debate,
        int round) =>
        new()
        {
            SystemPrompt = PromptLibrary.DebateSystemPrompt,
            Requirement = input.Text,
            Context = context,
            ThreadHistory = threadHistory,
            AdditionalContext = BuildComparisonContext(codexOpening, claudeOpening, review, debate, round),
            ModelOverride = input.Models?.OpenAi,
            ReasoningEffort = input.Models?.OpenAiReasoningEffort ?? "medium",
        };

    private static ProviderPromptRequest BuildAgreementPlanRequest(
        ConversationInput input,
        GitHubContextSnapshot? context,
        IReadOnlyList<ThreadMessage>? threadHistory,
        PlanningArtifact codexOpening,
        PlanningArtifact claudeOpening,
        ReviewArtifact review,
        DebateArtifact? debate) =>
        new()
        {
            SystemPrompt = PromptLibrary.AgreementPlannerSystemPrompt,
            Requirement = input.Text,
            Context = context,
            ThreadHistory = threadHistory,
            AdditionalContext = BuildAgreementContext(codexOpening, claudeOpening, review, debate),
            ModelOverride = input.Models?.OpenAi,
            ReasoningEffort = input.Models?.OpenAiReasoningEffort ?? "medium",
        };

    private static ProviderPromptRequest BuildSummaryRequest(
        ConversationInput input,
        GitHubContextSnapshot? context,
        IReadOnlyList<ThreadMessage>? threadHistory,
        PlanningArtifact codexOpening,
        PlanningArtifact? claudeOpening,
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
            AdditionalContext = BuildSummaryContext(codexOpening, claudeOpening),
            ModelOverride = input.Models?.OpenAi,
            ReasoningEffort = input.Models?.OpenAiReasoningEffort ?? "low",
        };

    private static OrchestrationResult BuildUserDecisionResult(
        GitHubContextSnapshot? context,
        PlanningArtifact codexOpening,
        PlanningArtifact? claudeOpening,
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
            CodexOpening = codexOpening,
            ClaudeOpening = claudeOpening,
            Plan = codexOpening,
            Review = review,
            Debate = debate,
            Summary = prompt.Trim(),
            NeedsUserDecision = true,
        };
    }

    private static OrchestrationResult BuildFallbackUserDecisionResult(
        GitHubContextSnapshot? context,
        PlanningArtifact codexOpening,
        PlanningArtifact? claudeOpening,
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
            CodexOpening = codexOpening,
            ClaudeOpening = claudeOpening,
            Plan = codexOpening,
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
            return "Claude comparison was skipped because the Anthropic API credit balance is too low for this run. The conversation continued with Codex only.";
        }

        return $"Claude comparison was skipped for this run. The conversation continued with Codex only. Details: {message}";
    }

    private static Dictionary<string, string>? BuildModelMetadata(string? modelId) =>
        string.IsNullOrWhiteSpace(modelId)
            ? null
            : new Dictionary<string, string>
            {
                ["model"] = modelId,
            };

    private static bool HasConsensus(ReviewArtifact review, DebateArtifact debate) =>
        review.IsAligned
        && debate.IsAligned
        && review.RequiresRepositoryAccess == debate.RequiresRepositoryAccess
        && review.RequiresImplementation == debate.RequiresImplementation
        && string.Equals(
            NormalizeBranch(review.SuggestedBranchName),
            NormalizeBranch(debate.SuggestedBranchName),
            StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeBranch(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string BuildComparisonContext(
        PlanningArtifact codexOpening,
        PlanningArtifact claudeOpening,
        ReviewArtifact? review,
        DebateArtifact? debate,
        int? round)
    {
        var lines = new List<string>();

        if (round is not null)
        {
            lines.Add($"Comparison round: {round.Value + 1}");
            lines.Add(string.Empty);
        }

        lines.Add("Codex initial response:");
        lines.Add(JsonExtraction.Serialize(codexOpening));
        lines.Add(string.Empty);
        lines.Add("Claude initial response:");
        lines.Add(JsonExtraction.Serialize(claudeOpening));

        if (review is not null && !string.IsNullOrWhiteSpace(review.Message))
        {
            lines.Add(string.Empty);
            lines.Add("Latest Claude comparison:");
            lines.Add(JsonExtraction.Serialize(review));
        }

        if (debate is not null && !string.IsNullOrWhiteSpace(debate.Message))
        {
            lines.Add(string.Empty);
            lines.Add("Latest Codex comparison:");
            lines.Add(JsonExtraction.Serialize(debate));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildAgreementContext(
        PlanningArtifact codexOpening,
        PlanningArtifact claudeOpening,
        ReviewArtifact review,
        DebateArtifact? debate)
    {
        var lines = new List<string>
        {
            "Initial openings:",
            $"Codex: {JsonExtraction.Serialize(codexOpening)}",
            $"Claude: {JsonExtraction.Serialize(claudeOpening)}",
            string.Empty,
            "Latest comparison state:",
            $"Claude: {JsonExtraction.Serialize(review)}",
        };

        if (debate is not null)
        {
            lines.Add($"Codex: {JsonExtraction.Serialize(debate)}");
        }

        lines.Add(string.Empty);
        lines.Add("Produce the agreed plan that both models can stand behind.");
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildSummaryContext(
        PlanningArtifact codexOpening,
        PlanningArtifact? claudeOpening)
    {
        var lines = new List<string>
        {
            "Initial model openings for reference:",
            $"Codex: {JsonExtraction.Serialize(codexOpening)}",
        };

        if (claudeOpening is not null)
        {
            lines.Add($"Claude: {JsonExtraction.Serialize(claudeOpening)}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildStageContent(string message, string? reasoning)
    {
        if (string.IsNullOrWhiteSpace(reasoning))
        {
            return message.Trim();
        }

        return string.Join(
            Environment.NewLine,
            [
                message.Trim(),
                string.Empty,
                reasoning.Trim(),
            ]);
    }
}
