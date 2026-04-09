namespace Assimalign.AI.Orchestrator.Core.Models;

public enum ThreadStageStatus
{
    Queued,
    Planning,
    Reviewing,
    Synthesizing,
    Completed,
    Failed,
}

public enum ThreadMessageRole
{
    User,
    Assistant,
    Stage,
    System,
}

public enum RepositoryWorkflowStatus
{
    Attached,
    ReadyForReview,
    Promoted,
    Failed,
}

public sealed class RepositoryTarget
{
    public string Owner { get; set; } = string.Empty;
    public string Repo { get; set; } = string.Empty;
    public string? Branch { get; set; }
    public string? BaseBranch { get; set; }
    public string? WorkingBranch { get; set; }
    public string? TargetBranch { get; set; }
    public string? DefaultBranch { get; set; }
    public string? Url { get; set; }
    public string? BranchUrl { get; set; }
    public string? CompareUrl { get; set; }
    public string? LastPromotionCommitSha { get; set; }
    public DateTimeOffset? PreparedAt { get; set; }
    public DateTimeOffset? PromotedAt { get; set; }
    public RepositoryWorkflowStatus WorkflowStatus { get; set; } = RepositoryWorkflowStatus.Attached;
    public int? IssueNumber { get; set; }
    public int? PullRequestNumber { get; set; }
}

public sealed class ConversationInput
{
    public string Text { get; set; } = string.Empty;
    public RepositoryTarget? Repository { get; set; }
    public ModelSelection? Models { get; set; }
}

public sealed class ConversationThread
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public ThreadStageStatus Status { get; set; } = ThreadStageStatus.Queued;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public RepositoryTarget? Repository { get; set; }
    public ModelSelection? Models { get; set; }
    public string LastMessagePreview { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Error { get; set; }
}

public sealed class ThreadMessage
{
    public string Id { get; set; } = string.Empty;
    public string ThreadId { get; set; } = string.Empty;
    public ThreadMessageRole Role { get; set; }
    public ThreadStageStatus? Stage { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

public sealed class ConversationThreadDetail
{
    public required ConversationThread Thread { get; init; }
    public required IReadOnlyList<ThreadMessage> Messages { get; init; }
}

public sealed class ProviderAvailability
{
    public bool OpenAi { get; init; }
    public bool Anthropic { get; init; }
}

public sealed class ModelSelection
{
    public string? OpenAi { get; set; }
    public string? Anthropic { get; set; }
}

public sealed class ModelOption
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
}

public sealed class ModelCatalog
{
    public IReadOnlyList<ModelOption> OpenAi { get; init; } = [];
    public IReadOnlyList<ModelOption> Anthropic { get; init; } = [];
    public ModelSelection Defaults { get; init; } = new();
}

public sealed class GitHubRepositoryReference
{
    public long Id { get; set; }
    public string Owner { get; set; } = string.Empty;
    public string Repo { get; set; } = string.Empty;
    public string DefaultBranch { get; set; } = string.Empty;
    public bool Private { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public sealed class GitHubIssueSnapshot
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public List<string> Labels { get; set; } = [];
    public string State { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public sealed class GitHubPullRequestSnapshot
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public sealed class GitHubContextSnapshot
{
    public RepositoryTarget Repository { get; set; } = new();
    public string? DefaultBranch { get; set; }
    public string? Description { get; set; }
    public string? Url { get; set; }
    public GitHubIssueSnapshot? Issue { get; set; }
    public GitHubPullRequestSnapshot? PullRequest { get; set; }
    public List<string> Notes { get; set; } = [];
}

public sealed class PlanningArtifact
{
    public string Message { get; set; } = string.Empty;
    public bool RequiresImplementation { get; set; }
    public string? SuggestedBranchName { get; set; }
}

public sealed class ReviewArtifact
{
    public string Message { get; set; } = string.Empty;
}

public sealed class OrchestrationResult
{
    public GitHubContextSnapshot? Context { get; set; }
    public RepositoryTarget? Repository { get; set; }
    public required PlanningArtifact Plan { get; init; }
    public required ReviewArtifact Review { get; init; }
    public required string Summary { get; init; }
}

public sealed class StageUpdate
{
    public ThreadStageStatus Status { get; init; }
    public ThreadMessage? Message { get; init; }
}

public sealed class OrchestrationJob
{
    public string ThreadId { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
}

public sealed class SpeechTokenResult
{
    public string Token { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Voice { get; set; } = string.Empty;
}

public sealed class GitHubBranchPreparationResult
{
    public required RepositoryTarget Repository { get; init; }
    public bool Created { get; init; }
    public string? SourceCommitSha { get; init; }
}

public sealed class GitHubPromotionResult
{
    public required RepositoryTarget Repository { get; init; }
    public string? MergeCommitSha { get; init; }
    public string Message { get; init; } = string.Empty;
}
