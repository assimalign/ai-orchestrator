using Assimalign.AI.Orchestrator.Application.Configuration;
using Assimalign.AI.Orchestrator.Application.Hosting;
using Assimalign.AI.Orchestrator.Application.Runtime;
using Assimalign.AI.Orchestrator.Core.Models;
using Assimalign.AI.Orchestrator.Application.Services;
using Assimalign.AI.Orchestrator.Infrastructure.Composition;
using Assimalign.AI.Orchestrator.Api;

var builder = WebApplication.CreateBuilder(args);
var settings = OrchestratorSettings.Load(builder.Configuration);

builder.Services.AddAssimalignAiOrchestratorTelemetry(
    builder.Configuration,
    "Assimalign.AI.Orchestrator.Api");
builder.Services.AddAssimalignAiOrchestratorApiPlatform(settings);
builder.Services.AddAssimalignAiOrchestratorRuntimeHandle(settings);

var app = builder.Build();
var runtimeHandle = app.Services.GetRequiredService<OrchestratorRuntimeHandle>();
runtimeHandle.EnsureStarted();

app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/healthz", () =>
{
    return runtimeHandle.State switch
    {
        "ready" => Results.Ok(new
        {
            ok = true,
            state = runtimeHandle.State,
            mode = settings.ExecutionMode,
        }),
        "failed" => Results.Problem(
            title: "Orchestrator runtime failed to initialize.",
            detail: runtimeHandle.ErrorMessage,
            statusCode: StatusCodes.Status503ServiceUnavailable),
        _ => Results.Ok(new
        {
            ok = false,
            state = runtimeHandle.State,
            mode = settings.ExecutionMode,
        }),
    };
})
.AllowAnonymous();

var api = app.MapGroup("/api").RequireAuthorization();

api.MapGet(
    "/config",
    async (CancellationToken cancellationToken) =>
        await WithRuntime(
            async currentRuntime => Results.Ok(new AppConfigResponse
            {
                ExecutionMode = currentRuntime.Settings.ExecutionMode,
                SpeechEnabled = !string.IsNullOrWhiteSpace(currentRuntime.Settings.AzureSpeechRegion),
                SpeechVoice = currentRuntime.Settings.SpeechVoice,
                Providers = currentRuntime.ProviderAvailability,
                Models = currentRuntime.Settings.BuildModelCatalog(),
                Connectors = await BuildConnectorsAsync(currentRuntime, cancellationToken),
            }),
            cancellationToken));

api.MapGet(
    "/threads",
    async (CancellationToken cancellationToken) =>
        await WithRuntime(
            async currentRuntime => Results.Ok(await currentRuntime.ThreadService.ListThreadsAsync(cancellationToken)),
            cancellationToken));

api.MapPost(
    "/threads",
    async (ConversationInput input, CancellationToken cancellationToken) =>
        await WithRuntime(
            async currentRuntime =>
        {
            if (string.IsNullOrWhiteSpace(input.Text))
            {
                return Results.BadRequest("Thread text is required.");
            }

            var detail = await currentRuntime.ThreadService.CreateThreadAsync(input, cancellationToken);
            return Results.Accepted($"/api/threads/{detail.Thread.Id}", detail);
        },
            cancellationToken));

api.MapGet(
    "/threads/{threadId}",
    async (string threadId, CancellationToken cancellationToken) =>
        await WithRuntime(
            async currentRuntime =>
            {
                var thread = await currentRuntime.ThreadService.GetThreadAsync(threadId, cancellationToken);
                return thread is null ? Results.NotFound() : Results.Ok(thread);
            },
            cancellationToken));

api.MapPost(
    "/threads/{threadId}/messages",
    async (
        string threadId,
        ConversationInput input,
        CancellationToken cancellationToken) =>
        await WithRuntime(
            async currentRuntime =>
        {
            if (string.IsNullOrWhiteSpace(input.Text))
            {
                return Results.BadRequest("Message text is required.");
            }

            try
            {
                var detail = await currentRuntime.ThreadService.AddMessageAsync(
                    threadId,
                    input,
                    cancellationToken);
                return Results.Accepted($"/api/threads/{detail.Thread.Id}", detail);
            }
            catch (InvalidOperationException error)
            {
                return Results.NotFound(error.Message);
            }
        },
            cancellationToken));

api.MapPost(
    "/threads/{threadId}/promote",
    async (
        string threadId,
        CancellationToken cancellationToken) =>
        await WithRuntime(
            async currentRuntime =>
            {
                try
                {
                    var detail = await currentRuntime.ThreadService.PromoteThreadAsync(
                        threadId,
                        cancellationToken);
                    return Results.Ok(detail);
                }
                catch (InvalidOperationException error)
                {
                    return Results.BadRequest(error.Message);
                }
            },
            cancellationToken));

api.MapGet(
    "/connectors",
    async (CancellationToken cancellationToken) =>
        await WithRuntime(
            async currentRuntime => Results.Ok(await BuildConnectorsAsync(currentRuntime, cancellationToken)),
            cancellationToken));

api.MapGet(
    "/connectors/{connectorId}/status",
    async (
        string connectorId,
        CancellationToken cancellationToken) =>
        await WithRuntime(
            async currentRuntime =>
            {
                if (!string.Equals(connectorId, "github", StringComparison.OrdinalIgnoreCase))
                {
                    return Results.NotFound();
                }

                return Results.Ok(await BuildGitHubConnectorStatusAsync(currentRuntime, cancellationToken));
            },
            cancellationToken));

api.MapGet(
    "/connectors/{connectorId}/repositories",
    async (
        string connectorId,
        CancellationToken cancellationToken) =>
        await WithRuntime(
            async currentRuntime =>
            {
                if (!string.Equals(connectorId, "github", StringComparison.OrdinalIgnoreCase))
                {
                    return Results.NotFound();
                }

                var repositories = await currentRuntime.GitHubContextService.ListRepositoriesAsync(cancellationToken);
                return Results.Ok(
                    repositories.Select(
                        repository => new ConnectorRepositoryReference
                        {
                            ConnectorId = "github",
                            Owner = repository.Owner,
                            Repo = repository.Repo,
                            DefaultBranch = repository.DefaultBranch,
                            Private = repository.Private,
                            Description = repository.Description,
                            Url = repository.Url,
                        }));
            },
            cancellationToken));

api.MapGet(
    "/connectors/{connectorId}/repositories/{owner}/{repo}/branches",
    async (
        string connectorId,
        string owner,
        string repo,
        CancellationToken cancellationToken) =>
        await WithRuntime(
            async currentRuntime =>
            {
                if (!string.Equals(connectorId, "github", StringComparison.OrdinalIgnoreCase))
                {
                    return Results.NotFound();
                }

                var branches = await currentRuntime.GitHubContextService.ListBranchesAsync(
                    owner,
                    repo,
                    cancellationToken);
                return Results.Ok(branches);
            },
            cancellationToken));

api.MapGet(
    "/github/repositories",
    async (CancellationToken cancellationToken) =>
        await WithRuntime(
            async currentRuntime => Results.Ok(await currentRuntime.GitHubContextService.ListRepositoriesAsync(cancellationToken)),
            cancellationToken));

api.MapGet(
    "/github/context",
    async (
        [AsParameters] GitHubContextQuery query,
        CancellationToken cancellationToken) =>
        await WithRuntime(
            async currentRuntime =>
        {
            if (string.IsNullOrWhiteSpace(query.Owner) || string.IsNullOrWhiteSpace(query.Repo))
            {
                return Results.BadRequest("owner and repo are required.");
            }

            var snapshot = await currentRuntime.GitHubContextService.BuildSnapshotAsync(
                query.ToRepositoryTarget(),
                cancellationToken);

            return Results.Ok(snapshot);
        },
            cancellationToken));

api.MapPost(
    "/speech/token",
    async (CancellationToken cancellationToken) =>
        await WithRuntime(
            async currentRuntime =>
            {
                var token = await currentRuntime.SpeechTokenService.GetTokenAsync(cancellationToken);
                return token is null
                    ? Results.BadRequest("Azure Speech credentials are not configured.")
                    : Results.Ok(token);
            },
            cancellationToken));

async Task<IResult> WithRuntime(
    Func<OrchestratorRuntime, Task<IResult>> action,
    CancellationToken cancellationToken)
{
    try
    {
        var currentRuntime = await runtimeHandle.GetRuntimeAsync(cancellationToken);
        return await action(currentRuntime);
    }
    catch (TimeoutException)
    {
        return Results.Problem(
            title: "Orchestrator runtime initialization timed out.",
            detail: "The API is still waiting on its backing services. Check the API container logs for startup errors.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception error)
    {
        return Results.Problem(
            title: "Orchestrator runtime initialization failed.",
            detail: error.GetBaseException().Message,
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}

static async Task<IReadOnlyList<ConnectorDefinition>> BuildConnectorsAsync(
    OrchestratorRuntime currentRuntime,
    CancellationToken cancellationToken)
{
    var authMode = GetGitHubAuthMode(currentRuntime.Settings);
    var hasRuntimeCredentials = !string.IsNullOrWhiteSpace(
        await currentRuntime.GitHubContextService.GetAccessTokenForRepositoryOperationsAsync(cancellationToken));

    return [
        new ConnectorDefinition
        {
            Id = "github",
            Label = "GitHub",
            Kind = "repository",
            Description = "Repository access through a GitHub App installation or runtime token.",
            AuthMode = authMode,
            Capabilities =
            [
                "List accessible repositories",
                "Create working branches",
                "Clone repositories into the execution workspace",
                "Commit and push code changes",
            ],
            SetupSummary = authMode == "GitHub App"
                ? "Requires GitHub App ID, installation ID, and the GitHub App private key in Key Vault."
                : "Requires a GitHub runtime token in Key Vault or a GitHub App configuration.",
            Enabled = hasRuntimeCredentials,
        },
    ];
}

static string GetGitHubAuthMode(OrchestratorSettings settings)
{
    if (!string.IsNullOrWhiteSpace(settings.GitHubAppId)
        && !string.IsNullOrWhiteSpace(settings.GitHubInstallationId))
    {
        return "GitHub App";
    }

    if (!string.IsNullOrWhiteSpace(settings.GitHubToken))
    {
        return "Personal access token";
    }

    return "Not configured";
}

static async Task<ConnectorStatusResponse> BuildGitHubConnectorStatusAsync(
    OrchestratorRuntime currentRuntime,
    CancellationToken cancellationToken)
{
    var authMode = GetGitHubAuthMode(currentRuntime.Settings);
    var accessToken = await currentRuntime.GitHubContextService.GetAccessTokenForRepositoryOperationsAsync(
        cancellationToken);

    if (string.IsNullOrWhiteSpace(accessToken))
    {
        return new ConnectorStatusResponse
        {
            Id = "github",
            Label = "GitHub",
            Enabled = false,
            Status = "configurationRequired",
            AuthMode = authMode,
            Message =
                "GitHub credentials are not available at runtime. Add either a GitHub App private key or a GitHub runtime token to Key Vault, then redeploy.",
        };
    }

    try
    {
        var repositories = await currentRuntime.GitHubContextService.ListRepositoriesAsync(cancellationToken);
        return new ConnectorStatusResponse
        {
            Id = "github",
            Label = "GitHub",
            Enabled = true,
            Status = "ready",
            AuthMode = authMode == "Not configured" ? "Runtime token" : authMode,
            RepositoryCount = repositories.Count,
            Message = repositories.Count > 0
                ? $"GitHub is connected and currently exposes {repositories.Count} repositories to the orchestrator."
                : "GitHub authenticated successfully, but this installation or token did not return any repositories.",
        };
    }
    catch (Exception error)
    {
        return new ConnectorStatusResponse
        {
            Id = "github",
            Label = "GitHub",
            Enabled = false,
            Status = "error",
            AuthMode = authMode,
            Message = error.GetBaseException().Message,
        };
    }
}

app.Run($"http://0.0.0.0:{settings.Port}");
