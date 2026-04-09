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

app.Run($"http://0.0.0.0:{settings.Port}");
