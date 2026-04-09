using Assimalign.AI.Orchestrator.Core.Models;

namespace Assimalign.AI.Orchestrator.Api;

public sealed class AppConfigResponse
{
    public string ExecutionMode { get; init; } = string.Empty;
    public bool SpeechEnabled { get; init; }
    public string SpeechVoice { get; init; } = string.Empty;
    public ProviderAvailability Providers { get; init; } = new();
    public ModelCatalog Models { get; init; } = new();
    public IReadOnlyList<ConnectorDefinition> Connectors { get; init; } = [];
}

public sealed class GitHubContextQuery
{
    public string Owner { get; init; } = string.Empty;
    public string Repo { get; init; } = string.Empty;
    public string? Branch { get; init; }
    public string? BaseBranch { get; init; }
    public string? WorkingBranch { get; init; }
    public string? TargetBranch { get; init; }
    public int? IssueNumber { get; init; }
    public int? PullRequestNumber { get; init; }

    public RepositoryTarget ToRepositoryTarget()
    {
        return new RepositoryTarget
        {
            Connector = "github",
            Owner = Owner,
            Repo = Repo,
            Branch = Branch,
            BaseBranch = BaseBranch,
            WorkingBranch = WorkingBranch,
            TargetBranch = TargetBranch,
            IssueNumber = IssueNumber,
            PullRequestNumber = PullRequestNumber,
        };
    }
}
