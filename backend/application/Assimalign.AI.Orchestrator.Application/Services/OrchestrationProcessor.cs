using Assimalign.AI.Orchestrator.Application.Abstractions.GitHub;
using Assimalign.AI.Orchestrator.Infrastructure.Storage;
using Assimalign.AI.Orchestrator.Core.Models;

namespace Assimalign.AI.Orchestrator.Application.Services;

public sealed class OrchestrationProcessor(
    IThreadRepository repository,
    OrchestrationEngine engine,
    IGitHubContextService gitHubContextService)
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

            thread.Status = ThreadStageStatus.Completed;
            thread.UpdatedAt = DateTimeOffset.UtcNow;
            thread.Summary = result.Summary;
            thread.LastMessagePreview = BuildPreview(result.Summary);
            thread.Error = null;

            if (thread.Repository is not null)
            {
                await PrepareWorkingBranchAsync(
                    thread,
                    result.Plan.SuggestedBranchName,
                    cancellationToken);
            }

            await repository.UpdateThreadAsync(thread, cancellationToken);

            await repository.AddMessageAsync(
                new ThreadMessage
                {
                    Id = Guid.NewGuid().ToString("D"),
                    ThreadId = threadId,
                    Role = ThreadMessageRole.Assistant,
                    Stage = ThreadStageStatus.Completed,
                    Title = "Codex",
                    Content = result.Summary,
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
}
