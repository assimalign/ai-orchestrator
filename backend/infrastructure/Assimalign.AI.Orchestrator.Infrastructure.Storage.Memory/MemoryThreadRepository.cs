using Assimalign.AI.Orchestrator.Core.Models;
using Assimalign.AI.Orchestrator.Infrastructure.Storage;

namespace Assimalign.AI.Orchestrator.Infrastructure.Storage.Memory;

public sealed class MemoryThreadRepository : IThreadRepository
{
    private readonly Dictionary<string, ConversationThread> threads = new();
    private readonly Dictionary<string, List<ThreadMessage>> messages = new();

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task CreateThreadAsync(
        ConversationThread thread,
        ThreadMessage initialMessage,
        CancellationToken cancellationToken = default)
    {
        threads[thread.Id] = CloneThread(thread);
        messages[thread.Id] = [CloneMessage(initialMessage)];
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ConversationThread>> ListThreadsAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var results = threads.Values
            .OrderByDescending(thread => thread.UpdatedAt)
            .Take(limit)
            .Select(CloneThread)
            .ToArray();

        return Task.FromResult<IReadOnlyList<ConversationThread>>(results);
    }

    public Task<ConversationThread?> GetThreadAsync(
        string threadId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            threads.TryGetValue(threadId, out var thread) ? CloneThread(thread) : null);
    }

    public Task<ConversationThreadDetail?> GetThreadDetailAsync(
        string threadId,
        CancellationToken cancellationToken = default)
    {
        if (!threads.TryGetValue(threadId, out var thread))
        {
            return Task.FromResult<ConversationThreadDetail?>(null);
        }

        var detail = new ConversationThreadDetail
        {
            Thread = CloneThread(thread),
            Messages = ListMessages(threadId, cancellationToken),
        };

        return Task.FromResult<ConversationThreadDetail?>(detail);
    }

    public Task UpdateThreadAsync(
        ConversationThread thread,
        CancellationToken cancellationToken = default)
    {
        threads[thread.Id] = CloneThread(thread);
        return Task.CompletedTask;
    }

    public Task DeleteThreadAsync(
        string threadId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        threads.Remove(threadId);
        messages.Remove(threadId);
        return Task.CompletedTask;
    }

    public Task UpdateMessageAsync(
        ThreadMessage message,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!messages.TryGetValue(message.ThreadId, out var list))
        {
            list = [];
            messages[message.ThreadId] = list;
        }

        var index = list.FindIndex(candidate => candidate.Id == message.Id);
        if (index >= 0)
        {
            list[index] = CloneMessage(message);
        }
        else
        {
            list.Add(CloneMessage(message));
        }

        return Task.CompletedTask;
    }

    public Task AddMessageAsync(ThreadMessage message, CancellationToken cancellationToken = default)
    {
        if (!messages.TryGetValue(message.ThreadId, out var list))
        {
            list = [];
            messages[message.ThreadId] = list;
        }

        list.Add(CloneMessage(message));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ThreadMessage>> ListMessagesAsync(
        string threadId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ListMessages(threadId, cancellationToken));
    }

    private IReadOnlyList<ThreadMessage> ListMessages(
        string threadId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!messages.TryGetValue(threadId, out var list))
        {
            return Array.Empty<ThreadMessage>();
        }

        return list
            .OrderBy(message => message.CreatedAt)
            .Select(CloneMessage)
            .ToArray();
    }

    private static ConversationThread CloneThread(ConversationThread thread) =>
        new()
        {
            Id = thread.Id,
            Title = thread.Title,
            Status = thread.Status,
            CreatedAt = thread.CreatedAt,
            UpdatedAt = thread.UpdatedAt,
            Repository = CloneRepository(thread.Repository),
            Models = CloneModels(thread.Models),
            LastMessagePreview = thread.LastMessagePreview,
            Summary = thread.Summary,
            Error = thread.Error,
        };

    private static RepositoryTarget? CloneRepository(RepositoryTarget? repository) =>
        repository is null
            ? null
            : new RepositoryTarget
            {
                Connector = repository.Connector,
                Owner = repository.Owner,
                Repo = repository.Repo,
                Branch = repository.Branch,
                BaseBranch = repository.BaseBranch,
                WorkingBranch = repository.WorkingBranch,
                TargetBranch = repository.TargetBranch,
                DefaultBranch = repository.DefaultBranch,
                Url = repository.Url,
                BranchUrl = repository.BranchUrl,
                CompareUrl = repository.CompareUrl,
                LastPromotionCommitSha = repository.LastPromotionCommitSha,
                PreparedAt = repository.PreparedAt,
                PromotedAt = repository.PromotedAt,
                WorkflowStatus = repository.WorkflowStatus,
                IssueNumber = repository.IssueNumber,
                PullRequestNumber = repository.PullRequestNumber,
            };

    private static ModelSelection? CloneModels(ModelSelection? models) =>
        models is null
            ? null
            : new ModelSelection
            {
                OpenAi = models.OpenAi,
                OpenAiReasoningEffort = models.OpenAiReasoningEffort,
                Anthropic = models.Anthropic,
                AnthropicReasoningEffort = models.AnthropicReasoningEffort,
            };

    private static ThreadMessage CloneMessage(ThreadMessage message) =>
        new()
        {
            Id = message.Id,
            ThreadId = message.ThreadId,
            Role = message.Role,
            Stage = message.Stage,
            Title = message.Title,
            Content = message.Content,
            Provider = message.Provider,
            CreatedAt = message.CreatedAt,
            Metadata = message.Metadata is null ? null : new Dictionary<string, string>(message.Metadata),
        };
}
