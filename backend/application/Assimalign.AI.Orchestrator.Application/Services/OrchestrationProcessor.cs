using Assimalign.AI.Orchestrator.Application.Abstractions.Execution;
using Assimalign.AI.Orchestrator.Application.Abstractions.GitHub;
using Assimalign.AI.Orchestrator.Infrastructure.Storage;
using Assimalign.AI.Orchestrator.Core.Models;

namespace Assimalign.AI.Orchestrator.Application.Services;

public sealed class OrchestrationProcessor(
    IThreadRepository repository,
    OrchestrationEngine engine,
    IGitHubContextService gitHubContextService,
    IRepositoryExecutionService repositoryExecutionService)
{
    public async Task<ConversationThreadDetail> ProcessAsync(
        string threadId,
        string messageId,
        CancellationToken cancellationToken = default)
    {
        var detail = await repository.GetThreadDetailAsync(threadId, cancellationToken)
            ?? throw new InvalidOperationException($"Thread '{threadId}' was not found.");

        var thread = detail.Thread;
        var sourceMessage = detail.Messages.FirstOrDefault(message => message.Id == messageId)
            ?? throw new InvalidOperationException($"Message '{messageId}' was not found on thread '{threadId}'.");
        var threadHistory = detail.Messages
            .Where(message => message.Id != messageId)
            .ToArray();

        try
        {
            var result = await engine.ExecuteAsync(
                new ConversationInput
                {
                    Text = sourceMessage.Content,
                    Repository = thread.Repository,
                    Models = thread.Models,
                },
                threadHistory,
                async update =>
                {
                    thread.Status = update.Status;
                    thread.UpdatedAt = DateTimeOffset.UtcNow;
                    await repository.UpdateThreadAsync(thread, cancellationToken);

                    if (update.Message is not null)
                    {
                        update.Message.ThreadId = threadId;
                        await repository.AddMessageAsync(update.Message, cancellationToken);
                    }
                },
                cancellationToken);

            thread.UpdatedAt = DateTimeOffset.UtcNow;
            thread.Summary = result.Summary;
            thread.LastMessagePreview = BuildPreview(result.Summary);
            thread.Error = null;

            if (thread.Repository is not null && result.Plan.RequiresImplementation && !result.NeedsUserDecision)
            {
                await PrepareWorkingBranchAsync(
                    thread,
                    result.Plan.SuggestedBranchName,
                    cancellationToken);

                if (thread.Repository?.WorkflowStatus is not RepositoryWorkflowStatus.Failed)
                {
                    var executionResult = await ExecuteRepositoryChangesAsync(
                        thread,
                        sourceMessage,
                        threadHistory,
                        result,
                        cancellationToken);

                    thread.Repository = executionResult.Repository;
                    thread.Summary = executionResult.Summary;
                    thread.LastMessagePreview = BuildPreview(executionResult.Summary);
                }
            }
            else if (thread.Repository is not null && ShouldInspectRepository(result.Plan, sourceMessage.Content))
            {
                var inspectionResult = await InspectRepositoryAsync(
                    thread,
                    sourceMessage,
                    threadHistory,
                    result,
                    cancellationToken);

                thread.Summary = inspectionResult.Summary;
                thread.LastMessagePreview = BuildPreview(inspectionResult.Summary);
            }

            thread.Status = ThreadStageStatus.Completed;

            await repository.UpdateThreadAsync(thread, cancellationToken);

            await repository.AddMessageAsync(
                new ThreadMessage
                {
                    Id = Guid.NewGuid().ToString("D"),
                    ThreadId = threadId,
                    Role = ThreadMessageRole.Assistant,
                    Stage = ThreadStageStatus.Completed,
                    Title = "Codex",
                    Content = thread.Summary ?? result.Summary,
                    Provider = "codex",
                    CreatedAt = DateTimeOffset.UtcNow,
                },
                cancellationToken);
        }
        catch (Exception error)
        {
            thread.Status = ThreadStageStatus.Failed;
            thread.UpdatedAt = DateTimeOffset.UtcNow;
            thread.Error = error.Message;
            thread.LastMessagePreview = BuildPreview(error.Message);
            await repository.UpdateThreadAsync(thread, cancellationToken);

            await repository.AddMessageAsync(
                new ThreadMessage
                {
                    Id = Guid.NewGuid().ToString("D"),
                    ThreadId = threadId,
                    Role = ThreadMessageRole.Stage,
                    Stage = ThreadStageStatus.Failed,
                    Title = "Run failed",
                    Content = error.Message,
                    CreatedAt = DateTimeOffset.UtcNow,
                },
                cancellationToken);
        }

        return await repository.GetThreadDetailAsync(threadId, cancellationToken)
            ?? throw new InvalidOperationException($"Thread '{threadId}' disappeared after processing.");
    }

    private static string BuildPreview(string text)
    {
        var normalized = text.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= 120 ? normalized : $"{normalized[..117]}...";
    }

    private static bool ShouldInspectRepository(
        PlanningArtifact plan,
        string messageContent)
    {
        if (plan.RequiresImplementation || plan.RequiresRepositoryAccess)
        {
            return !plan.RequiresImplementation;
        }

        var normalized = messageContent.ToLowerInvariant();
        return normalized.Contains("review")
            || normalized.Contains("inspect")
            || normalized.Contains("summar")
            || normalized.Contains("synops")
            || normalized.Contains("structure")
            || normalized.Contains("walk me through")
            || normalized.Contains("look through")
            || normalized.Contains("read through")
            || normalized.Contains("understand the repo")
            || normalized.Contains("understand the codebase");
    }

    private async Task<RepositoryInspectionResult> InspectRepositoryAsync(
        ConversationThread thread,
        ThreadMessage sourceMessage,
        IReadOnlyList<ThreadMessage> threadHistory,
        OrchestrationResult result,
        CancellationToken cancellationToken)
    {
        await repository.AddMessageAsync(
            new ThreadMessage
            {
                Id = Guid.NewGuid().ToString("D"),
                ThreadId = thread.Id,
                Role = ThreadMessageRole.Stage,
                Stage = ThreadStageStatus.Synthesizing,
                Title = "Repository inspection started",
                Content = "Codex is cloning the selected repository and reading the relevant files for a synopsis.",
                Provider = "codex",
                CreatedAt = DateTimeOffset.UtcNow,
            },
            cancellationToken);

        var inspectionResult = await repositoryExecutionService.InspectAsync(
            new ConversationInput
            {
                Text = sourceMessage.Content,
                Models = thread.Models,
                Repository = thread.Repository,
            },
            thread.Repository!,
            result,
            threadHistory,
            cancellationToken);

        await repository.AddMessageAsync(
            new ThreadMessage
            {
                Id = Guid.NewGuid().ToString("D"),
                ThreadId = thread.Id,
                Role = ThreadMessageRole.Stage,
                Stage = ThreadStageStatus.Completed,
                Title = "Repository inspection completed",
                Content = BuildInspectionMessage(inspectionResult),
                Provider = "codex",
                CreatedAt = DateTimeOffset.UtcNow,
            },
            cancellationToken);

        return inspectionResult;
    }

    private async Task<RepositoryExecutionResult> ExecuteRepositoryChangesAsync(
        ConversationThread thread,
        ThreadMessage sourceMessage,
        IReadOnlyList<ThreadMessage> threadHistory,
        OrchestrationResult result,
        CancellationToken cancellationToken)
    {
        var executionActivityMessages = new Dictionary<string, (string MessageId, DateTimeOffset CreatedAt)>(StringComparer.OrdinalIgnoreCase);

        await repository.AddMessageAsync(
            new ThreadMessage
            {
                Id = Guid.NewGuid().ToString("D"),
                ThreadId = thread.Id,
                Role = ThreadMessageRole.Stage,
                Stage = ThreadStageStatus.Synthesizing,
                Title = "Repository execution started",
                Content = "Codex is cloning the working branch, preparing file edits, and running verification.",
                Provider = "codex",
                CreatedAt = DateTimeOffset.UtcNow,
            },
            cancellationToken);

        var executionResult = await repositoryExecutionService.ExecuteAsync(
            new ConversationInput
            {
                Text = sourceMessage.Content,
                Models = thread.Models,
                Repository = thread.Repository,
            },
            thread.Repository!,
            result,
            threadHistory,
            update => UpsertExecutionActivityAsync(
                thread.Id,
                update,
                executionActivityMessages,
                cancellationToken),
            cancellationToken);

        await repository.AddMessageAsync(
            new ThreadMessage
            {
                Id = Guid.NewGuid().ToString("D"),
                ThreadId = thread.Id,
                Role = ThreadMessageRole.Stage,
                Stage = ThreadStageStatus.Completed,
                Title = "Repository changes pushed",
                Content = BuildExecutionMessage(executionResult),
                Provider = "github",
                CreatedAt = DateTimeOffset.UtcNow,
            },
            cancellationToken);

        return executionResult;
    }

    private async Task UpsertExecutionActivityAsync(
        string threadId,
        ExecutionActivityUpdate update,
        Dictionary<string, (string MessageId, DateTimeOffset CreatedAt)> activityMessages,
        CancellationToken cancellationToken)
    {
        if (activityMessages.TryGetValue(update.ActivityId, out var existingMessage))
        {
            await repository.UpdateMessageAsync(
                new ThreadMessage
                {
                    Id = existingMessage.MessageId,
                    ThreadId = threadId,
                    Role = ThreadMessageRole.Stage,
                    Stage = update.Stage,
                    Title = update.Title,
                    Content = update.Content,
                    Provider = update.Provider,
                    CreatedAt = existingMessage.CreatedAt,
                    Metadata = update.Metadata,
                },
                cancellationToken);

            return;
        }

        var newMessageId = Guid.NewGuid().ToString("D");
        var createdAt = DateTimeOffset.UtcNow;
        activityMessages[update.ActivityId] = (newMessageId, createdAt);

        await repository.AddMessageAsync(
            new ThreadMessage
            {
                Id = newMessageId,
                ThreadId = threadId,
                Role = ThreadMessageRole.Stage,
                Stage = update.Stage,
                Title = update.Title,
                Content = update.Content,
                Provider = update.Provider,
                CreatedAt = createdAt,
                Metadata = update.Metadata,
            },
            cancellationToken);
    }

    private async Task PrepareWorkingBranchAsync(
        ConversationThread thread,
        string? suggestedBranchName,
        CancellationToken cancellationToken)
    {
        try
        {
            var branchResult = await gitHubContextService.EnsureWorkingBranchAsync(
                thread.Repository!,
                suggestedBranchName,
                cancellationToken);

            thread.Repository = branchResult.Repository;
            await repository.AddMessageAsync(
                new ThreadMessage
                {
                    Id = Guid.NewGuid().ToString("D"),
                    ThreadId = thread.Id,
                    Role = ThreadMessageRole.Stage,
                    Stage = ThreadStageStatus.Completed,
                    Title = branchResult.Created ? "Prepared working branch" : "Attached working branch",
                    Content = BuildBranchPreparationMessage(branchResult.Repository, branchResult),
                    Provider = "github",
                    CreatedAt = DateTimeOffset.UtcNow,
                },
                cancellationToken);
        }
        catch (Exception error)
        {
            thread.Repository!.WorkflowStatus = RepositoryWorkflowStatus.Failed;
            await repository.AddMessageAsync(
                new ThreadMessage
                {
                    Id = Guid.NewGuid().ToString("D"),
                    ThreadId = thread.Id,
                    Role = ThreadMessageRole.Stage,
                    Stage = ThreadStageStatus.Failed,
                    Title = "GitHub branch workflow unavailable",
                    Content = error.Message,
                    Provider = "github",
                    CreatedAt = DateTimeOffset.UtcNow,
                },
                cancellationToken);
        }
    }

    private static string BuildBranchPreparationMessage(
        RepositoryTarget repository,
        GitHubBranchPreparationResult branchResult)
    {
        var lines = new List<string>
        {
            $"Repository: {repository.Owner}/{repository.Repo}",
            $"Base branch: {repository.BaseBranch ?? repository.DefaultBranch}",
            $"Working branch: {repository.WorkingBranch ?? repository.Branch}",
            $"Target branch: {repository.TargetBranch ?? repository.BaseBranch}",
            branchResult.Created ? "A new working branch was created for this thread." : "The existing working branch was attached to this thread.",
        };

        if (!string.IsNullOrWhiteSpace(branchResult.SourceCommitSha))
        {
            lines.Add($"Source commit: {branchResult.SourceCommitSha}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildExecutionMessage(RepositoryExecutionResult executionResult)
    {
        var lines = new List<string>
        {
            $"Repository: {executionResult.Repository.Owner}/{executionResult.Repository.Repo}",
            $"Working branch: {executionResult.Repository.WorkingBranch ?? executionResult.Repository.Branch}",
            $"Commit: {executionResult.CommitSha}",
            $"Commit message: {executionResult.CommitMessage}",
        };

        if (executionResult.ChangedFiles.Count > 0)
        {
            lines.Add($"Changed files: {string.Join(", ", executionResult.ChangedFiles)}");
        }

        foreach (var testResult in executionResult.TestResults)
        {
            lines.Add($"Verified: {testResult.Command}");
        }

        if (!string.IsNullOrWhiteSpace(executionResult.Repository.CompareUrl))
        {
            lines.Add($"Review: {executionResult.Repository.CompareUrl}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildInspectionMessage(RepositoryInspectionResult inspectionResult)
    {
        var lines = new List<string>
        {
            $"Repository: {inspectionResult.Repository.Owner}/{inspectionResult.Repository.Repo}",
        };

        if (inspectionResult.SelectedFiles.Count > 0)
        {
            lines.Add($"Inspected files: {string.Join(", ", inspectionResult.SelectedFiles)}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
