using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Assimalign.AI.Orchestrator.Application.Abstractions.GitHub;
using Assimalign.AI.Orchestrator.Application.Abstractions.Providers;
using Assimalign.AI.Orchestrator.Application.Configuration;
using Assimalign.AI.Orchestrator.Core.Models;
using Assimalign.AI.Orchestrator.Core.Utilities;
using Microsoft.IdentityModel.Tokens;

namespace Assimalign.AI.Orchestrator.Infrastructure.Integrations.GitHub;

public sealed class GitHubContextService : IGitHubContextService
{
    private readonly HttpClient apiClient;
    private readonly HttpClient appClient;
    private readonly OrchestratorSettings settings;
    private readonly ISecretProvider secrets;
    private string? cachedInstallationToken;
    private DateTimeOffset cachedInstallationTokenExpiresAt;

    public GitHubContextService(OrchestratorSettings settings, ISecretProvider secrets)
    {
        this.settings = settings;
        this.secrets = secrets;

        apiClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.github.com/"),
        };
        apiClient.DefaultRequestHeaders.UserAgent.ParseAdd("ai-dev-orchestrator");
        apiClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        appClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.github.com/"),
        };
        appClient.DefaultRequestHeaders.UserAgent.ParseAdd("ai-dev-orchestrator");
        appClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<IReadOnlyList<GitHubRepositoryReference>> ListRepositoriesAsync(
        CancellationToken cancellationToken = default)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            return Array.Empty<GitHubRepositoryReference>();
        }

        var installationResponse = await SendAsync(
            "installation/repositories?per_page=100",
            token,
            cancellationToken,
            allowNotFound: true);

        if (installationResponse is not null)
        {
            using var document = JsonDocument.Parse(installationResponse);
            return document.RootElement
                .GetProperty("repositories")
                .EnumerateArray()
                .Select(MapRepository)
                .ToArray();
        }

        var userResponse = await SendAsync("user/repos?per_page=100&sort=updated", token, cancellationToken);
        if (string.IsNullOrWhiteSpace(userResponse))
        {
            return Array.Empty<GitHubRepositoryReference>();
        }

        using var userDocument = JsonDocument.Parse(userResponse);
        return userDocument.RootElement.EnumerateArray().Select(MapRepository).ToArray();
    }

    public Task<string?> GetAccessTokenForRepositoryOperationsAsync(
        CancellationToken cancellationToken = default) =>
        GetAccessTokenAsync(cancellationToken);

    public async Task<GitHubContextSnapshot?> BuildSnapshotAsync(
        RepositoryTarget? target,
        CancellationToken cancellationToken = default)
    {
        if (target is null || string.IsNullOrWhiteSpace(target.Owner) || string.IsNullOrWhiteSpace(target.Repo))
        {
            return null;
        }

        var snapshot = new GitHubContextSnapshot
        {
            Repository = new RepositoryTarget
            {
                Connector = string.IsNullOrWhiteSpace(target.Connector) ? "github" : target.Connector,
                Owner = target.Owner,
                Repo = target.Repo,
                Branch = target.Branch,
                IssueNumber = target.IssueNumber,
                PullRequestNumber = target.PullRequestNumber,
            },
        };

        var token = await GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            snapshot.Notes.Add("GitHub credentials are not configured, so only the requested repository coordinates were included.");
            return snapshot;
        }

        var repositoryJson = await SendAsync(
            $"repos/{target.Owner}/{target.Repo}",
            token,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(repositoryJson))
        {
            snapshot.Notes.Add("GitHub repository details could not be loaded.");
            return snapshot;
        }

        using (var document = JsonDocument.Parse(repositoryJson))
        {
            var repository = document.RootElement;
            snapshot.DefaultBranch = repository.GetProperty("default_branch").GetString();
            snapshot.Description = repository.GetProperty("description").GetString();
            snapshot.Url = repository.GetProperty("html_url").GetString();
            snapshot.Repository.DefaultBranch = snapshot.DefaultBranch;
            snapshot.Repository.Url = snapshot.Url;
        }

        if (target.IssueNumber.HasValue)
        {
            var issueJson = await SendAsync(
                $"repos/{target.Owner}/{target.Repo}/issues/{target.IssueNumber.Value}",
                token,
                cancellationToken);
            if (string.IsNullOrWhiteSpace(issueJson))
            {
                snapshot.Notes.Add($"Issue #{target.IssueNumber.Value} could not be loaded.");
            }
            else
            {
                using var issueDocument = JsonDocument.Parse(issueJson);
                var issue = issueDocument.RootElement;
                snapshot.Issue = new GitHubIssueSnapshot
                {
                    Number = issue.GetProperty("number").GetInt32(),
                    Title = issue.GetProperty("title").GetString() ?? string.Empty,
                    Body = issue.GetProperty("body").GetString() ?? string.Empty,
                    State = issue.GetProperty("state").GetString() ?? string.Empty,
                    Url = issue.GetProperty("html_url").GetString() ?? string.Empty,
                    Labels = issue.GetProperty("labels")
                        .EnumerateArray()
                        .Select(label =>
                        {
                            return label.ValueKind == JsonValueKind.String
                                ? label.GetString() ?? string.Empty
                                : label.TryGetProperty("name", out var nameProperty)
                                    ? nameProperty.GetString() ?? string.Empty
                                    : string.Empty;
                        })
                        .Where(label => !string.IsNullOrWhiteSpace(label))
                        .ToList(),
                };
            }
        }

        if (target.PullRequestNumber.HasValue)
        {
            var pullRequestJson = await SendAsync(
                $"repos/{target.Owner}/{target.Repo}/pulls/{target.PullRequestNumber.Value}",
                token,
                cancellationToken);
            if (string.IsNullOrWhiteSpace(pullRequestJson))
            {
                snapshot.Notes.Add($"Pull request #{target.PullRequestNumber.Value} could not be loaded.");
            }
            else
            {
                using var pullRequestDocument = JsonDocument.Parse(pullRequestJson);
                var pullRequest = pullRequestDocument.RootElement;
                snapshot.PullRequest = new GitHubPullRequestSnapshot
                {
                    Number = pullRequest.GetProperty("number").GetInt32(),
                    Title = pullRequest.GetProperty("title").GetString() ?? string.Empty,
                    Body = pullRequest.GetProperty("body").GetString() ?? string.Empty,
                    State = pullRequest.GetProperty("state").GetString() ?? string.Empty,
                    Url = pullRequest.GetProperty("html_url").GetString() ?? string.Empty,
                };
            }
        }

        return snapshot;
    }

    public async Task<GitHubBranchPreparationResult> EnsureWorkingBranchAsync(
        RepositoryTarget target,
        string? preferredBranchName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(target.Owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(target.Repo);

        var token = await GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                "GitHub write credentials are not configured, so a working branch cannot be prepared.");
        }

        var repositoryJson = await SendAsync($"repos/{target.Owner}/{target.Repo}", token, cancellationToken)
            ?? throw new InvalidOperationException("GitHub repository details could not be loaded.");

        using var repositoryDocument = JsonDocument.Parse(repositoryJson);
        var repository = repositoryDocument.RootElement;
        var defaultBranch = repository.GetProperty("default_branch").GetString() ?? "main";
        var htmlUrl = repository.GetProperty("html_url").GetString() ?? $"https://github.com/{target.Owner}/{target.Repo}";

        var baseBranch = FirstNonEmpty(target.BaseBranch, target.TargetBranch, target.Branch, defaultBranch)
            ?? defaultBranch;
        var targetBranch = FirstNonEmpty(target.TargetBranch, baseBranch)
            ?? baseBranch;
        var workingBranch = NormalizeBranchName(target.WorkingBranch, preferredBranchName, target.Repo);

        var baseRefJson = await SendAsync(
            $"repos/{target.Owner}/{target.Repo}/git/ref/heads/{baseBranch}",
            token,
            cancellationToken)
            ?? throw new InvalidOperationException($"Base branch '{baseBranch}' could not be loaded from GitHub.");

        using var baseRefDocument = JsonDocument.Parse(baseRefJson);
        var baseSha = baseRefDocument.RootElement.GetProperty("object").GetProperty("sha").GetString()
            ?? throw new InvalidOperationException($"Base branch '{baseBranch}' does not expose a commit SHA.");

        var workingRefJson = await SendAsync(
            $"repos/{target.Owner}/{target.Repo}/git/ref/heads/{workingBranch}",
            token,
            cancellationToken,
            allowNotFound: true);

        var created = false;
        if (string.IsNullOrWhiteSpace(workingRefJson))
        {
            using var request = CreateJsonRequest(
                HttpMethod.Post,
                $"repos/{target.Owner}/{target.Repo}/git/refs",
                token,
                new
                {
                    @ref = $"refs/heads/{workingBranch}",
                    sha = baseSha,
                });

            using var response = await apiClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"GitHub could not create working branch '{workingBranch}': {(int)response.StatusCode} {responseBody}");
            }

            created = true;
        }

        var preparedRepository = CloneRepositoryTarget(target);
        preparedRepository.BaseBranch = baseBranch;
        preparedRepository.Branch = workingBranch;
        preparedRepository.WorkingBranch = workingBranch;
        preparedRepository.TargetBranch = targetBranch;
        preparedRepository.DefaultBranch = defaultBranch;
        preparedRepository.Url = htmlUrl;
        preparedRepository.BranchUrl = $"{htmlUrl}/tree/{workingBranch}";
        preparedRepository.CompareUrl = $"{htmlUrl}/compare/{targetBranch}...{workingBranch}";
        preparedRepository.PreparedAt = DateTimeOffset.UtcNow;
        preparedRepository.WorkflowStatus = RepositoryWorkflowStatus.ReadyForReview;

        return new GitHubBranchPreparationResult
        {
            Repository = preparedRepository,
            Created = created,
            SourceCommitSha = baseSha,
        };
    }

    public async Task<GitHubPromotionResult> PromoteBranchAsync(
        RepositoryTarget target,
        string commitMessage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(target.Owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(target.Repo);

        var token = await GetAccessTokenAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                "GitHub write credentials are not configured, so the working branch cannot be promoted.");
        }

        var workingBranch = FirstNonEmpty(target.WorkingBranch, target.Branch);
        var targetBranch = FirstNonEmpty(target.TargetBranch, target.BaseBranch, target.DefaultBranch);

        if (string.IsNullOrWhiteSpace(workingBranch) || string.IsNullOrWhiteSpace(targetBranch))
        {
            throw new InvalidOperationException(
                "A working branch and target branch must both exist before promotion can happen.");
        }

        using var request = CreateJsonRequest(
            HttpMethod.Post,
            $"repos/{target.Owner}/{target.Repo}/merges",
            token,
            new
            {
                @base = targetBranch,
                head = workingBranch,
                commit_message = commitMessage,
            });

        using var response = await apiClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            throw new InvalidOperationException(
                $"GitHub reported merge conflicts while promoting '{workingBranch}' into '{targetBranch}'.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"GitHub could not promote '{workingBranch}' into '{targetBranch}': {(int)response.StatusCode} {body}");
        }

        string? mergeCommitSha = null;
        var mergeMessage = $"Promoted '{workingBranch}' into '{targetBranch}'.";
        if (!string.IsNullOrWhiteSpace(body))
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("sha", out var shaProperty))
            {
                mergeCommitSha = shaProperty.GetString();
            }

            if (document.RootElement.TryGetProperty("message", out var messageProperty))
            {
                mergeMessage = messageProperty.GetString() ?? mergeMessage;
            }
        }

        var promotedRepository = CloneRepositoryTarget(target);
        promotedRepository.WorkflowStatus = RepositoryWorkflowStatus.Promoted;
        promotedRepository.PromotedAt = DateTimeOffset.UtcNow;
        promotedRepository.LastPromotionCommitSha = mergeCommitSha;

        return new GitHubPromotionResult
        {
            Repository = promotedRepository,
            MergeCommitSha = mergeCommitSha,
            Message = mergeMessage,
        };
    }

    private async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var directToken = await secrets.GetAsync(
            settings.GitHubTokenSecretName,
            settings.GitHubToken,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(directToken))
        {
            return directToken;
        }

        if (cachedInstallationToken is not null && cachedInstallationTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            return cachedInstallationToken;
        }

        if (string.IsNullOrWhiteSpace(settings.GitHubAppId)
            || string.IsNullOrWhiteSpace(settings.GitHubInstallationId))
        {
            return null;
        }

        var privateKey = await secrets.GetAsync(
            settings.GitHubAppPrivateKeySecretName,
            settings.GitHubAppPrivateKey,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(privateKey))
        {
            return null;
        }

        var appJwt = CreateGitHubAppJwt(settings.GitHubAppId, privateKey);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"app/installations/{settings.GitHubInstallationId}/access_tokens");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", appJwt);
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        using var response = await appClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"GitHub App authentication failed with {(int)response.StatusCode}: {body}");
        }

        using var document = JsonDocument.Parse(body);
        cachedInstallationToken = document.RootElement.GetProperty("token").GetString();
        cachedInstallationTokenExpiresAt = document.RootElement.GetProperty("expires_at").GetDateTimeOffset();
        return cachedInstallationToken;
    }

    private static string CreateGitHubAppJwt(string appId, string privateKey)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKey);

        var signingCredentials = new SigningCredentials(
            new RsaSecurityKey(rsa),
            SecurityAlgorithms.RsaSha256);

        var now = DateTimeOffset.UtcNow;
        var payload = new JwtPayload
        {
            { "iat", now.AddMinutes(-1).ToUnixTimeSeconds() },
            { "exp", now.AddMinutes(9).ToUnixTimeSeconds() },
            { "iss", appId },
        };

        var token = new JwtSecurityToken(new JwtHeader(signingCredentials), payload);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<string?> SendAsync(
        string path,
        string token,
        CancellationToken cancellationToken,
        bool allowNotFound = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await apiClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (allowNotFound && response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"GitHub request '{path}' failed with {(int)response.StatusCode}: {body}");
        }

        return body;
    }

    private static HttpRequestMessage CreateJsonRequest<TBody>(
        HttpMethod method,
        string path,
        string token,
        TBody body)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(body, options: JsonDefaults.Options);
        return request;
    }

    private static RepositoryTarget CloneRepositoryTarget(RepositoryTarget source) =>
        new()
        {
            Connector = source.Connector,
            Owner = source.Owner,
            Repo = source.Repo,
            Branch = source.Branch,
            BaseBranch = source.BaseBranch,
            WorkingBranch = source.WorkingBranch,
            TargetBranch = source.TargetBranch,
            DefaultBranch = source.DefaultBranch,
            Url = source.Url,
            BranchUrl = source.BranchUrl,
            CompareUrl = source.CompareUrl,
            LastPromotionCommitSha = source.LastPromotionCommitSha,
            PreparedAt = source.PreparedAt,
            PromotedAt = source.PromotedAt,
            WorkflowStatus = source.WorkflowStatus,
            IssueNumber = source.IssueNumber,
            PullRequestNumber = source.PullRequestNumber,
        };

    private static string NormalizeBranchName(
        string? currentBranch,
        string? preferredBranchName,
        string repoName)
    {
        var raw = FirstNonEmpty(currentBranch, preferredBranchName)
            ?? $"codex/{repoName}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        var normalized = raw.Trim().ToLowerInvariant();
        normalized = Regex.Replace(normalized, @"[^a-z0-9/_-]+", "-");
        normalized = Regex.Replace(normalized, @"-{2,}", "-");
        normalized = normalized.Trim('-', '/', '.');

        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = $"codex/{repoName.ToLowerInvariant()}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        }

        if (!normalized.Contains('/'))
        {
            normalized = $"codex/{normalized}";
        }

        return normalized;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static GitHubRepositoryReference MapRepository(JsonElement repository)
    {
        var owner = repository.GetProperty("owner").GetProperty("login").GetString() ?? string.Empty;

        return new GitHubRepositoryReference
        {
            Id = repository.GetProperty("id").GetInt64(),
            Owner = owner,
            Repo = repository.GetProperty("name").GetString() ?? string.Empty,
            DefaultBranch = repository.GetProperty("default_branch").GetString() ?? string.Empty,
            Private = repository.GetProperty("private").GetBoolean(),
            Description = repository.GetProperty("description").GetString() ?? string.Empty,
            Url = repository.GetProperty("html_url").GetString() ?? string.Empty,
        };
    }
}
