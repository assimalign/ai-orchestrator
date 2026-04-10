using Assimalign.AI.Orchestrator.Core.Models;

namespace Assimalign.AI.Orchestrator.Infrastructure.Storage;

public interface IThreadRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task CreateThreadAsync(
        ConversationThread thread,
        ThreadMessage initialMessage,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConversationThread>> ListThreadsAsync(
        int limit = 50,
        CancellationToken cancellationToken = default);
    Task<ConversationThread?> GetThreadAsync(
        string threadId,
        CancellationToken cancellationToken = default);
    Task<ConversationThreadDetail?> GetThreadDetailAsync(
        string threadId,
        CancellationToken cancellationToken = default);
    Task UpdateThreadAsync(
        ConversationThread thread,
        CancellationToken cancellationToken = default);
    Task DeleteThreadAsync(
        string threadId,
        CancellationToken cancellationToken = default);
    Task UpdateMessageAsync(
        ThreadMessage message,
        CancellationToken cancellationToken = default);
    Task AddMessageAsync(ThreadMessage message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ThreadMessage>> ListMessagesAsync(
        string threadId,
        CancellationToken cancellationToken = default);
}
