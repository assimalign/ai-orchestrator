using System.Text.Json;
using Assimalign.AI.Orchestrator.Core.Models;
using Assimalign.AI.Orchestrator.Core.Utilities;
using Assimalign.AI.Orchestrator.Infrastructure.Storage;
using Azure;
using Azure.Data.Tables;

namespace Assimalign.AI.Orchestrator.Infrastructure.Storage.Tables;

public sealed class TableThreadRepository(string connectionString, string tableName) : IThreadRepository
{
    private readonly TableClient client = new(connectionString, tableName);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await client.CreateIfNotExistsAsync(cancellationToken);
        }
        catch (RequestFailedException error) when (error.Status == 409)
        {
        }
    }

    public async Task CreateThreadAsync(
        ConversationThread thread,
        ThreadMessage initialMessage,
        CancellationToken cancellationToken = default)
    {
        await client.UpsertEntityAsync(ToThreadEntity(thread), TableUpdateMode.Replace, cancellationToken);
        await client.UpsertEntityAsync(ToMessageEntity(initialMessage), TableUpdateMode.Replace, cancellationToken);
    }

    public async Task<IReadOnlyList<ConversationThread>> ListThreadsAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var threads = new List<ConversationThread>();

        await foreach (var entity in client.QueryAsync<TableEntity>(
                           filter: $"PartitionKey eq '{ThreadPartitionKey}'",
                           cancellationToken: cancellationToken))
        {
            threads.Add(FromThreadEntity(entity));
        }

        return threads
            .OrderByDescending(thread => thread.UpdatedAt)
            .Take(limit)
            .ToArray();
    }

    public async Task<ConversationThread?> GetThreadAsync(
        string threadId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await client.GetEntityAsync<TableEntity>(
                ThreadPartitionKey,
                threadId,
                cancellationToken: cancellationToken);

            return FromThreadEntity(response.Value);
        }
        catch (RequestFailedException error) when (error.Status == 404)
        {
            return null;
        }
    }

    public async Task<ConversationThreadDetail?> GetThreadDetailAsync(
        string threadId,
        CancellationToken cancellationToken = default)
    {
        var thread = await GetThreadAsync(threadId, cancellationToken);
        if (thread is null)
        {
            return null;
        }

        var messages = await ListMessagesAsync(threadId, cancellationToken);
        return new ConversationThreadDetail
        {
            Thread = thread,
            Messages = messages,
        };
    }

    public Task UpdateThreadAsync(
        ConversationThread thread,
        CancellationToken cancellationToken = default) =>
        client.UpsertEntityAsync(ToThreadEntity(thread), TableUpdateMode.Replace, cancellationToken);

    public Task AddMessageAsync(ThreadMessage message, CancellationToken cancellationToken = default) =>
        client.UpsertEntityAsync(ToMessageEntity(message), TableUpdateMode.Replace, cancellationToken);

    public async Task<IReadOnlyList<ThreadMessage>> ListMessagesAsync(
        string threadId,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<ThreadMessage>();

        await foreach (var entity in client.QueryAsync<TableEntity>(
                           filter: $"PartitionKey eq '{GetMessagePartitionKey(threadId)}'",
                           cancellationToken: cancellationToken))
        {
            messages.Add(FromMessageEntity(entity));
        }

        return messages.OrderBy(message => message.CreatedAt).ToArray();
    }

    private static readonly string ThreadPartitionKey = "thread";

    private static string GetMessagePartitionKey(string threadId) => $"message:{threadId}";

    private static TableEntity ToThreadEntity(ConversationThread thread)
    {
        return new TableEntity(ThreadPartitionKey, thread.Id)
        {
            ["title"] = thread.Title,
            ["status"] = thread.Status.ToString(),
            ["createdAt"] = thread.CreatedAt.ToString("O"),
            ["updatedAt"] = thread.UpdatedAt.ToString("O"),
            ["repositoryJson"] = JsonSerializer.Serialize(thread.Repository, JsonDefaults.Options),
            ["modelsJson"] = JsonSerializer.Serialize(thread.Models, JsonDefaults.Options),
            ["lastMessagePreview"] = thread.LastMessagePreview,
            ["summary"] = thread.Summary ?? string.Empty,
            ["error"] = thread.Error ?? string.Empty,
        };
    }

    private static ConversationThread FromThreadEntity(TableEntity entity)
    {
        return new ConversationThread
        {
            Id = entity.RowKey,
            Title = entity.GetString("title") ?? string.Empty,
            Status = Enum.Parse<ThreadStageStatus>(entity.GetString("status") ?? nameof(ThreadStageStatus.Queued), true),
            CreatedAt = DateTimeOffset.Parse(entity.GetString("createdAt") ?? DateTimeOffset.UtcNow.ToString("O")),
            UpdatedAt = DateTimeOffset.Parse(entity.GetString("updatedAt") ?? DateTimeOffset.UtcNow.ToString("O")),
            Repository = Deserialize<RepositoryTarget>(entity.GetString("repositoryJson")),
            Models = Deserialize<ModelSelection>(entity.GetString("modelsJson")),
            LastMessagePreview = entity.GetString("lastMessagePreview") ?? string.Empty,
            Summary = EmptyToNull(entity.GetString("summary")),
            Error = EmptyToNull(entity.GetString("error")),
        };
    }

    private static TableEntity ToMessageEntity(ThreadMessage message)
    {
        return new TableEntity(GetMessagePartitionKey(message.ThreadId), message.Id)
        {
            ["threadId"] = message.ThreadId,
            ["role"] = message.Role.ToString(),
            ["stage"] = message.Stage?.ToString() ?? string.Empty,
            ["title"] = message.Title,
            ["content"] = message.Content,
            ["provider"] = message.Provider ?? string.Empty,
            ["createdAt"] = message.CreatedAt.ToString("O"),
            ["metadataJson"] = JsonSerializer.Serialize(message.Metadata, JsonDefaults.Options),
        };
    }

    private static ThreadMessage FromMessageEntity(TableEntity entity)
    {
        var stage = entity.GetString("stage");
        return new ThreadMessage
        {
            Id = entity.RowKey,
            ThreadId = entity.GetString("threadId") ?? string.Empty,
            Role = Enum.Parse<ThreadMessageRole>(entity.GetString("role") ?? nameof(ThreadMessageRole.System), true),
            Stage = string.IsNullOrWhiteSpace(stage)
                ? null
                : Enum.Parse<ThreadStageStatus>(stage, true),
            Title = entity.GetString("title") ?? string.Empty,
            Content = entity.GetString("content") ?? string.Empty,
            Provider = EmptyToNull(entity.GetString("provider")),
            CreatedAt = DateTimeOffset.Parse(entity.GetString("createdAt") ?? DateTimeOffset.UtcNow.ToString("O")),
            Metadata = Deserialize<Dictionary<string, string>>(entity.GetString("metadataJson")),
        };
    }

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json, JsonDefaults.Options);
    }
}
