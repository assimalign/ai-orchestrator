using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Assimalign.AI.Orchestrator.Application.Abstractions.Execution;
using Assimalign.AI.Orchestrator.Application.Abstractions.Providers;
using Assimalign.AI.Orchestrator.Application.Configuration;
using Assimalign.AI.Orchestrator.Core.Models;
using Assimalign.AI.Orchestrator.Infrastructure.Integrations.GitHub;

namespace Assimalign.AI.Orchestrator.Infrastructure.Execution;

public sealed class RepositoryExecutionService(
    OrchestratorSettings settings,
    IOpenAiOrchestrationClient openAiClient,
    GitHubContextService gitHubContextService) : IRepositoryExecutionService
{
    private const int MaxRepositoryTreeEntries = 500;
    private const int MaxSelectedFiles = 12;
    private const int MaxFileContentCharacters = 40_000;

    public async Task<RepositoryExecutionResult> ExecuteAsync(
        ConversationInput input,
        RepositoryTarget repository,
        OrchestrationResult orchestration,
        IReadOnlyList<ThreadMessage>? threadHistory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(repository);

        if (string.IsNullOrWhiteSpace(repository.Owner) || string.IsNullOrWhiteSpace(repository.Repo))
        {
            throw new InvalidOperationException("A repository owner and name are required before execution can start.");
        }

        if (string.IsNullOrWhiteSpace(repository.WorkingBranch))
        {
            throw new InvalidOperationException("A working branch must be prepared before repository execution can start.");
        }

        var accessToken = await gitHubContextService.GetAccessTokenForRepositoryOperationsAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("GitHub write credentials are not configured, so repository execution cannot clone or push.");
        }

        Directory.CreateDirectory(settings.RepositoryWorkspaceRoot);

        var workspacePath = Path.Combine(
            settings.RepositoryWorkspaceRoot,
            $"{repository.Owner}-{repository.Repo}-{Guid.NewGuid():N}");

        try
        {
            await CloneRepositoryAsync(repository, accessToken, workspacePath, cancellationToken);
            await ConfigureGitIdentityAsync(workspacePath, cancellationToken);

            var repositoryTree = await BuildRepositoryTreeAsync(workspacePath, cancellationToken);
            var executionEnvironment = BuildExecutionEnvironmentDescription();
            var executionContext = await openAiClient.CreateExecutionContextAsync(
                input.Text,
                orchestration,
                repositoryTree,
                executionEnvironment,
                threadHistory,
                input.Models?.OpenAi,
                cancellationToken);

            var selectedFiles = NormalizeSelectedFiles(executionContext.SelectedFiles);
            var fileContents = LoadSelectedFileContents(workspacePath, selectedFiles);
            var executionArtifact = await openAiClient.CreateExecutionArtifactAsync(
                input.Text,
                orchestration,
                executionContext,
                repositoryTree,
                executionEnvironment,
                fileContents,
                threadHistory,
                input.Models?.OpenAi,
                cancellationToken);

            var normalizedArtifact = NormalizeExecutionArtifact(executionContext, executionArtifact);
            ApplyChanges(workspacePath, normalizedArtifact.Changes);

            var changedFiles = await GetChangedFilesAsync(workspacePath, cancellationToken);
            if (changedFiles.Count == 0)
            {
                throw new InvalidOperationException("Codex did not produce any repository file changes to commit.");
            }

            var setupResults = await RunShellCommandsAsync(
                workspacePath,
                normalizedArtifact.SetupCommands,
                cancellationToken);
            EnsureCommandsSucceeded(setupResults, "Setup");

            var testResults = await RunShellCommandsAsync(
                workspacePath,
                normalizedArtifact.TestCommands,
                cancellationToken);
            EnsureCommandsSucceeded(testResults, "Verification");

            await RunGitAsync(workspacePath, cancellationToken, "add", "-A");

            var stagedFiles = await GetStagedFilesAsync(workspacePath, cancellationToken);
            if (stagedFiles.Count == 0)
            {
                throw new InvalidOperationException("Repository changes were applied locally but nothing was staged for commit.");
            }

            var commitMessage = string.IsNullOrWhiteSpace(normalizedArtifact.CommitMessage)
                ? executionContext.CommitMessage
                : normalizedArtifact.CommitMessage.Trim();
            if (string.IsNullOrWhiteSpace(commitMessage))
            {
                commitMessage = $"Update {repository.Repo} via Assimalign AI Orchestrator";
            }

            await RunGitAsync(workspacePath, cancellationToken, "commit", "-m", commitMessage);
            var commitSha = (await RunGitForOutputAsync(workspacePath, cancellationToken, "rev-parse", "HEAD")).Trim();
            await RunGitAsync(
                workspacePath,
                cancellationToken,
                "push",
                "origin",
                $"HEAD:{repository.WorkingBranch}");

            var updatedRepository = CloneRepository(repository);
            updatedRepository.WorkflowStatus = RepositoryWorkflowStatus.ReadyForReview;

            return new RepositoryExecutionResult
            {
                Repository = updatedRepository,
                CommitSha = commitSha,
                CommitMessage = commitMessage,
                Summary = BuildExecutionSummary(updatedRepository, normalizedArtifact, commitSha, stagedFiles, testResults),
                ChangedFiles = stagedFiles,
                SetupResults = setupResults,
                TestResults = testResults,
            };
        }
        finally
        {
            TryDeleteWorkspace(workspacePath);
        }
    }

    private async Task CloneRepositoryAsync(
        RepositoryTarget repository,
        string accessToken,
        string workspacePath,
        CancellationToken cancellationToken)
    {
        var baseBranch = repository.BaseBranch
            ?? repository.TargetBranch
            ?? repository.DefaultBranch
            ?? "main";
        var cloneUrl =
            $"https://x-access-token:{Uri.EscapeDataString(accessToken)}@github.com/{repository.Owner}/{repository.Repo}.git";

        await RunGitAsync(
            settings.RepositoryWorkspaceRoot,
            cancellationToken,
            "clone",
            "--branch",
            baseBranch,
            "--single-branch",
            cloneUrl,
            workspacePath);

        await RunGitAsync(workspacePath, cancellationToken, "fetch", "origin", repository.WorkingBranch!);
        await RunGitAsync(
            workspacePath,
            cancellationToken,
            "checkout",
            "-B",
            repository.WorkingBranch!,
            $"origin/{repository.WorkingBranch}");
    }

    private async Task ConfigureGitIdentityAsync(
        string workspacePath,
        CancellationToken cancellationToken)
    {
        await RunGitAsync(workspacePath, cancellationToken, "config", "user.name", settings.GitCommitUserName);
        await RunGitAsync(workspacePath, cancellationToken, "config", "user.email", settings.GitCommitUserEmail);
    }

    private async Task<string> BuildRepositoryTreeAsync(
        string workspacePath,
        CancellationToken cancellationToken)
    {
        var output = await RunGitForOutputAsync(workspacePath, cancellationToken, "ls-files");
        var entries = output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(path => !ShouldIgnorePath(path))
            .Take(MaxRepositoryTreeEntries + 1)
            .ToArray();

        var lines = entries.Take(MaxRepositoryTreeEntries).Select(path => $"- {path}").ToList();
        if (entries.Length > MaxRepositoryTreeEntries)
        {
            lines.Add("- ... truncated ...");
        }

        return lines.Count == 0
            ? "- Repository is empty."
            : string.Join(Environment.NewLine, lines);
    }

    private string BuildExecutionEnvironmentDescription()
    {
        var shell = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "PowerShell"
            : "bash";
        var operatingSystem = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "Windows"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? "Linux"
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? "macOS"
                    : RuntimeInformation.OSDescription;
        var baseTools = new List<string> { "git", ".NET SDK", "node", "npm" };

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            baseTools.AddRange(["apt-get", "python3", "pip3", "curl", "jq", "make"]);
        }

        var lines = new List<string>
        {
            $"Operating system: {operatingSystem}",
            $"Shell: {shell}",
            $"Workspace root: {settings.RepositoryWorkspaceRoot}",
            "Execution workspace is isolated per run and may be modified freely.",
            $"Base tools already available: {string.Join(", ", baseTools)}.",
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            lines.Add("Use native Windows/PowerShell commands when you need setup steps.");
        }
        else
        {
            lines.Add("The Linux worker container runs as root, so missing tooling may be installed directly with apt-get when needed.");
        }

        lines.Add("Keep setup/tooling installs minimal and limited to what this repository needs to build or test.");
        return string.Join(Environment.NewLine, lines);
    }

    private IReadOnlyDictionary<string, string> LoadSelectedFileContents(
        string workspacePath,
        IReadOnlyList<string> selectedFiles)
    {
        var contents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var relativePath in selectedFiles)
        {
            var absolutePath = ResolveRepositoryPath(workspacePath, relativePath);
            if (!File.Exists(absolutePath))
            {
                continue;
            }

            var text = File.ReadAllText(absolutePath);
            if (text.Length > MaxFileContentCharacters)
            {
                text = string.Concat(
                    text[..MaxFileContentCharacters],
                    Environment.NewLine,
                    "... FILE TRUNCATED ...");
            }

            contents[relativePath] = text;
        }

        return contents;
    }

    private static IReadOnlyList<string> NormalizeSelectedFiles(IEnumerable<string>? selectedFiles)
    {
        return (selectedFiles ?? [])
            .Select(path => path.Replace('\\', '/').Trim())
            .Where(path =>
                !string.IsNullOrWhiteSpace(path)
                && !Path.IsPathRooted(path)
                && !path.Contains("..", StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxSelectedFiles)
            .ToArray();
    }

    private static RepositoryExecutionArtifact NormalizeExecutionArtifact(
        RepositoryExecutionContextArtifact executionContext,
        RepositoryExecutionArtifact artifact)
    {
        artifact.CommitMessage = string.IsNullOrWhiteSpace(artifact.CommitMessage)
            ? executionContext.CommitMessage
            : artifact.CommitMessage.Trim();
        artifact.SetupCommands = NormalizeCommands(
            artifact.SetupCommands.Count > 0 ? artifact.SetupCommands : executionContext.SetupCommands);
        artifact.TestCommands = NormalizeCommands(
            artifact.TestCommands.Count > 0 ? artifact.TestCommands : executionContext.TestCommands);
        artifact.Changes = NormalizeChanges(artifact.Changes);
        return artifact;
    }

    private static List<string> NormalizeCommands(IEnumerable<string>? commands) =>
        (commands ?? [])
            .Select(command => command.Trim())
            .Where(command => !string.IsNullOrWhiteSpace(command))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<RepositoryFileChange> NormalizeChanges(IEnumerable<RepositoryFileChange>? changes) =>
        (changes ?? [])
            .Where(change => change is not null)
            .Select(
                change => new RepositoryFileChange
                {
                    Path = change.Path.Replace('\\', '/').Trim(),
                    Operation = string.IsNullOrWhiteSpace(change.Operation)
                        ? "upsert"
                        : change.Operation.Trim().ToLowerInvariant(),
                    Content = change.Content,
                })
            .Where(change =>
                !string.IsNullOrWhiteSpace(change.Path)
                && !Path.IsPathRooted(change.Path)
                && !change.Path.Contains("..", StringComparison.Ordinal)
                && (change.Operation == "upsert" || change.Operation == "delete"))
            .DistinctBy(change => change.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static void ApplyChanges(
        string workspacePath,
        IEnumerable<RepositoryFileChange> changes)
    {
        foreach (var change in changes)
        {
            var absolutePath = ResolveRepositoryPath(workspacePath, change.Path);

            if (change.Operation == "delete")
            {
                if (File.Exists(absolutePath))
                {
                    File.Delete(absolutePath);
                }

                continue;
            }

            if (change.Content is null)
            {
                throw new InvalidOperationException($"Codex returned an upsert for '{change.Path}' without file content.");
            }

            var directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(
                absolutePath,
                change.Content,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }

    private async Task<IReadOnlyList<string>> GetChangedFilesAsync(
        string workspacePath,
        CancellationToken cancellationToken)
    {
        var output = await RunGitForOutputAsync(workspacePath, cancellationToken, "status", "--short");
        return output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Length > 3 ? line[3..].Trim() : line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<IReadOnlyList<string>> GetStagedFilesAsync(
        string workspacePath,
        CancellationToken cancellationToken)
    {
        var output = await RunGitForOutputAsync(workspacePath, cancellationToken, "diff", "--cached", "--name-only");
        return output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<IReadOnlyList<CommandExecutionResult>> RunShellCommandsAsync(
        string workspacePath,
        IReadOnlyList<string> commands,
        CancellationToken cancellationToken)
    {
        var results = new List<CommandExecutionResult>();

        foreach (var command in commands)
        {
            results.Add(await RunShellCommandAsync(workspacePath, command, cancellationToken));
        }

        return results;
    }

    private async Task<CommandExecutionResult> RunShellCommandAsync(
        string workspacePath,
        string command,
        CancellationToken cancellationToken)
    {
        var fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "powershell" : "/bin/bash";
        string[] arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ["-NoProfile", "-NonInteractive", "-Command", command]
            : ["-lc", command];

        return await RunProcessAsync(workspacePath, fileName, arguments, cancellationToken);
    }

    private async Task RunGitAsync(
        string workingDirectory,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        var result = await RunProcessAsync(workingDirectory, "git", arguments, cancellationToken);
        if (result.ExitCode == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Git command failed: git {string.Join(' ', arguments)}{Environment.NewLine}{FormatCommandFailure(result)}");
    }

    private async Task<string> RunGitForOutputAsync(
        string workingDirectory,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        var result = await RunProcessAsync(workingDirectory, "git", arguments, cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Git command failed: git {string.Join(' ', arguments)}{Environment.NewLine}{FormatCommandFailure(result)}");
        }

        return result.StandardOutput;
    }

    private async Task<CommandExecutionResult> RunProcessAsync(
        string workingDirectory,
        string fileName,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(settings.RepositoryCommandTimeoutSeconds));

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKillProcess(process);
            throw new TimeoutException(
                $"The command '{fileName} {string.Join(' ', arguments)}' timed out after {settings.RepositoryCommandTimeoutSeconds} seconds.");
        }

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;

        return new CommandExecutionResult
        {
            Command = string.Join(' ', [fileName, .. arguments]),
            ExitCode = process.ExitCode,
            StandardOutput = standardOutput.Trim(),
            StandardError = standardError.Trim(),
        };
    }

    private static void EnsureCommandsSucceeded(
        IReadOnlyList<CommandExecutionResult> results,
        string phase)
    {
        var failure = results.FirstOrDefault(result => result.ExitCode != 0);
        if (failure is null)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{phase} command failed: {failure.Command}{Environment.NewLine}{FormatCommandFailure(failure)}");
    }

    private static string BuildExecutionSummary(
        RepositoryTarget repository,
        RepositoryExecutionArtifact artifact,
        string commitSha,
        IReadOnlyList<string> changedFiles,
        IReadOnlyList<CommandExecutionResult> testResults)
    {
        var parts = new List<string>
        {
            string.IsNullOrWhiteSpace(artifact.Message)
                ? $"Implemented the requested change on `{repository.WorkingBranch ?? repository.Branch}`."
                : artifact.Message.Trim(),
            $"Committed and pushed `{commitSha[..Math.Min(7, commitSha.Length)]}` to `{repository.WorkingBranch ?? repository.Branch}`.",
        };

        if (changedFiles.Count > 0)
        {
            parts.Add($"Changed files: {string.Join(", ", changedFiles.Take(6))}{(changedFiles.Count > 6 ? ", ..." : string.Empty)}.");
        }

        if (testResults.Count > 0)
        {
            parts.Add($"Verification passed with {testResults.Count} command{(testResults.Count == 1 ? string.Empty : "s")}.");
        }

        if (!string.IsNullOrWhiteSpace(repository.CompareUrl))
        {
            parts.Add($"Review: {repository.CompareUrl}");
        }

        return string.Join(Environment.NewLine, parts);
    }

    private static string FormatCommandFailure(CommandExecutionResult result)
    {
        var output = string.Join(
            Environment.NewLine,
            new[]
            {
                result.StandardOutput,
                result.StandardError,
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

        return string.IsNullOrWhiteSpace(output) ? $"Exit code: {result.ExitCode}" : output;
    }

    private static RepositoryTarget CloneRepository(RepositoryTarget source) =>
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

    private static bool ShouldIgnorePath(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Any(
            segment => segment.Equals("node_modules", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("bin", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("obj", StringComparison.OrdinalIgnoreCase)
                || segment.Equals("dist", StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveRepositoryPath(string workspacePath, string relativePath)
    {
        var absolutePath = Path.GetFullPath(Path.Combine(workspacePath, relativePath));
        var rootPath = Path.GetFullPath(workspacePath);

        if (!absolutePath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Repository path '{relativePath}' escapes the execution workspace.");
        }

        return absolutePath;
    }

    private static void TryDeleteWorkspace(string workspacePath)
    {
        try
        {
            if (Directory.Exists(workspacePath))
            {
                Directory.Delete(workspacePath, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort kill only.
        }
    }
}
