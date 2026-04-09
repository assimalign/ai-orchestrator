using Assimalign.AI.Orchestrator.Application.Abstractions.GitHub;
using Assimalign.AI.Orchestrator.Infrastructure.Storage;
using Assimalign.AI.Orchestrator.Infrastructure.Messaging;
using Assimalign.AI.Orchestrator.Core.Models;

namespace Assimalign.AI.Orchestrator.Application.Services;

public sealed class ThreadConversationService(
    IThreadRepository repository,
    OrchestrationProcessor processor,
    IGitHubContextService gitHubContextService,
    IOrchestrationQueue? queue,
    bool processInline)
{
    public Task<IReadOnlyList<ConversationThread>> ListThreadsAsync(
        CancellationToken cancellationToken = default) =>
        repository.ListThreadsAsync(cancellationToken: cancellationToken);

    public Task<ConversationThreadDetail?> GetThreadAsync(
        string threadId,
        CancellationToken cancellationToken = default) =>
        repository.GetThreadDetailAsync(threadId, cancellationToken);

    public async Task<ConversationThreadDetail> CreateThreadAsync(
        ConversationInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Text);

        var now = DateTimeOffset.UtcNow;
        var thread = new ConversationThread
        {
            Id = Guid.NewGuid().ToString("D"),
            Title = BuildTitle(input.Text),
            Status = ThreadStageStatus.Queued,
            CreatedAt = now,
            UpdatedAt = now,
            Repository = PrepareRepositoryTarget(input.Repository),
            Models = PrepareModelSelection(input.Models),
            LastMessagePreview = BuildPreview(input.Text),
        };

        var message = CreateUserMessage(thread.Id, input.Text, now);
        await repository.CreateThreadAsync(thread, message, cancellationToken);
        await DispatchAsync(thread.Id, message.Id, cancellationToken);

        return await repository.GetThreadDetailAsync(thread.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Thread '{thread.Id}' was not created.");
    }

    public async Task<ConversationThreadDetail> AddMessageAsync(
        string threadId,
        ConversationInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Text);

        var thread = await repository.GetThreadAsync(threadId, cancellationToken)
            ?? throw new InvalidOperationException($"Thread '{threadId}' was not found.");

        var now = DateTimeOffset.UtcNow;
        thread.Status = ThreadStageStatus.Queued;
        thread.UpdatedAt = now;
        thread.LastMessagePreview = BuildPreview(input.Text);
        thread.Error = null;

        if (input.Repository is not null)
        {
            thread.Repository = MergeRepositoryTargets(thread.Repository, input.Repository);
        }

        if (input.Models is not null)
        {
            thread.Models = MergeModelSelections(thread.Models, input.Models);
        }

        var message = CreateUserMessage(threadId, input.Text, now);

        await repository.UpdateThreadAsync(thread, cancellationToken);
        await repository.AddMessageAsync(message, cancellationToken);
        await DispatchAsync(threadId, message.Id, cancellationToken);

        return await repository.GetThreadDetailAsync(threadId, cancellationToken)
            ?? throw new InvalidOperationException($"Thread '{threadId}' disappeared after updating.");
    }

    public async Task<ConversationThreadDetail> PromoteThreadAsync(
        string threadId,
        CancellationToken cancellationToken = default)
    {
        var thread = await repository.GetThreadAsync(threadId, cancellationToken)
            ?? throw new InvalidOperationException($"Thread '{threadId}' was not found.");

        if (thread.Repository is null)
        {
            throw new InvalidOperationException("This thread is not attached to a GitHub repository.");
        }

        try
        {
            var result = await gitHubContextService.PromoteBranchAsync(
                thread.Repository,
                $"Promote {thread.Repository.WorkingBranch ?? thread.Repository.Branch} into {thread.Repository.TargetBranch ?? thread.Repository.BaseBranch}",
                cancellationToken);

            thread.Repository = result.Repository;
            thread.UpdatedAt = DateTimeOffset.UtcNow;
            thread.Error = null;
            thread.LastMessagePreview = BuildPreview(result.Message);

            await repository.UpdateThreadAsync(thread, cancellationToken);
            await repository.AddMessageAsync(
                new ThreadMessage
                {
                    Id = Guid.NewGuid().ToString("D"),
                    ThreadId = threadId,
                    Role = ThreadMessageRole.Stage,
                    Stage = ThreadStageStatus.Completed,
                    Title = "Promoted working branch",
                    Content = BuildPromotionMessage(result.Repository, result),
                    Provider = "github",
                    CreatedAt = DateTimeOffset.UtcNow,
                },
                cancellationToken);
        }
        catch (Exception error)
        {
            thread.Repository.WorkflowStatus = RepositoryWorkflowStatus.Failed;
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
                    Title = "Promotion failed",
                    Content = error.Message,
                    Provider = "github",
                    CreatedAt = DateTimeOffset.UtcNow,
                },
                cancellationToken);

            throw;
        }

        return await repository.GetThreadDetailAsync(threadId, cancellationToken)
            ?? throw new InvalidOperationException($"Thread '{threadId}' disappeared after promotion.");
    }

    private async Task DispatchAsync(
        string threadId,
        string messageId,
        CancellationToken cancellationToken)
    {
        if (processInline || queue is null)
        {
            await processor.ProcessAsync(threadId, messageId, cancellationToken);
            return;
        }

        await queue.EnqueueAsync(
            new OrchestrationJob
            {
                ThreadId = threadId,
                MessageId = messageId,
            },
            cancellationToken);
    }

    private static ThreadMessage CreateUserMessage(string threadId, string text, DateTimeOffset createdAt) =>
        new()
        {
            Id = Guid.NewGuid().ToString("D"),
            ThreadId = threadId,
            Role = ThreadMessageRole.User,
            Title = "You",
            Content = text.Trim(),
            CreatedAt = createdAt,
        };

    private static RepositoryTarget? PrepareRepositoryTarget(RepositoryTarget? repository)
    {
        if (repository is null)
        {
            return null;
        }

        repository.WorkflowStatus = RepositoryWorkflowStatus.Attached;
        repository.TargetBranch ??= repository.BaseBranch ?? repository.Branch;
        return repository;
    }

    private static ModelSelection? PrepareModelSelection(ModelSelection? models)
    {
        if (models is null)
        {
            return null;
        }

        var prepared = new ModelSelection
        {
            OpenAi = string.IsNullOrWhiteSpace(models.OpenAi) ? null : models.OpenAi.Trim(),
            Anthropic = string.IsNullOrWhiteSpace(models.Anthropic) ? null : models.Anthropic.Trim(),
        };

        return string.IsNullOrWhiteSpace(prepared.OpenAi)
            && string.IsNullOrWhiteSpace(prepared.Anthropic)
            ? null
            : prepared;
    }

    private static RepositoryTarget MergeRepositoryTargets(
        RepositoryTarget? existing,
        RepositoryTarget incoming)
    {
        if (existing is null
            || !string.Equals(existing.Owner, incoming.Owner, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(existing.Repo, incoming.Repo, StringComparison.OrdinalIgnoreCase))
        {
            return PrepareRepositoryTarget(incoming) ?? incoming;
        }

        existing.Owner = incoming.Owner;
        existing.Repo = incoming.Repo;
        existing.BaseBranch = incoming.BaseBranch ?? existing.BaseBranch ?? existing.TargetBranch ?? existing.Branch;
        existing.TargetBranch = incoming.TargetBranch ?? existing.TargetBranch ?? existing.BaseBranch ?? existing.Branch;

        if (!string.IsNullOrWhiteSpace(incoming.Branch))
        {
            existing.Branch = incoming.Branch;
        }

        if (!string.IsNullOrWhiteSpace(incoming.WorkingBranch))
        {
            existing.WorkingBranch = incoming.WorkingBranch;
        }

        return existing;
    }

    private static ModelSelection MergeModelSelections(
        ModelSelection? existing,
        ModelSelection incoming)
    {
        var prepared = PrepareModelSelection(incoming) ?? new ModelSelection();
        if (existing is null)
        {
            return prepared;
        }

        existing.OpenAi = prepared.OpenAi ?? existing.OpenAi;
        existing.Anthropic = prepared.Anthropic ?? existing.Anthropic;
        return existing;
    }

    private static string BuildTitle(string text)
    {
        var normalized = text.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= 54 ? normalized : $"{normalized[..51]}...";
    }

    private static string BuildPreview(string text)
    {
        var normalized = text.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= 120 ? normalized : $"{normalized[..117]}...";
    }

    private static string BuildPromotionMessage(
        RepositoryTarget repository,
        GitHubPromotionResult result)
    {
        var lines = new List<string>
        {
            $"Repository: {repository.Owner}/{repository.Repo}",
            $"Working branch: {repository.WorkingBranch ?? repository.Branch}",
            $"Target branch: {repository.TargetBranch ?? repository.BaseBranch}",
            result.Message,
        };

        if (!string.IsNullOrWhiteSpace(result.MergeCommitSha))
        {
            lines.Add($"Merge commit: {result.MergeCommitSha}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
