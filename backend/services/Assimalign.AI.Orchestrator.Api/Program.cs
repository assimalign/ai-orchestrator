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
builder.Services.AddAssimalignAiOrchestratorRuntime(settings);

var app = builder.Build();

app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/healthz", (OrchestratorRuntime currentRuntime) => Results.Ok(new
{
    ok = true,
    mode = currentRuntime.Settings.ExecutionMode,
}))
.AllowAnonymous();

var api = app.MapGroup("/api").RequireAuthorization();

api.MapGet("/config", (OrchestratorRuntime currentRuntime) => Results.Ok(new AppConfigResponse
{
    ExecutionMode = currentRuntime.Settings.ExecutionMode,
    SpeechEnabled = !string.IsNullOrWhiteSpace(currentRuntime.Settings.AzureSpeechRegion),
    SpeechVoice = currentRuntime.Settings.SpeechVoice,
    Providers = currentRuntime.ProviderAvailability,
    Models = currentRuntime.Settings.BuildModelCatalog(),
}));

api.MapGet(
    "/threads",
    async (OrchestratorRuntime currentRuntime, CancellationToken cancellationToken) =>
        Results.Ok(await currentRuntime.ThreadService.ListThreadsAsync(cancellationToken)));

api.MapPost(
    "/threads",
    async (ConversationInput input, OrchestratorRuntime currentRuntime, CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(input.Text))
        {
            return Results.BadRequest("Thread text is required.");
        }

        var detail = await currentRuntime.ThreadService.CreateThreadAsync(input, cancellationToken);
        return Results.Accepted($"/api/threads/{detail.Thread.Id}", detail);
    });

api.MapGet(
    "/threads/{threadId}",
    async (string threadId, OrchestratorRuntime currentRuntime, CancellationToken cancellationToken) =>
    {
        var thread = await currentRuntime.ThreadService.GetThreadAsync(threadId, cancellationToken);
        return thread is null ? Results.NotFound() : Results.Ok(thread);
    });

api.MapPost(
    "/threads/{threadId}/messages",
    async (
        string threadId,
        ConversationInput input,
        OrchestratorRuntime currentRuntime,
        CancellationToken cancellationToken) =>
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
    });

api.MapPost(
    "/threads/{threadId}/promote",
    async (
        string threadId,
        OrchestratorRuntime currentRuntime,
        CancellationToken cancellationToken) =>
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
    });

api.MapGet(
    "/github/repositories",
    async (OrchestratorRuntime currentRuntime, CancellationToken cancellationToken) =>
        Results.Ok(await currentRuntime.GitHubContextService.ListRepositoriesAsync(cancellationToken)));

api.MapGet(
    "/github/context",
    async (
        [AsParameters] GitHubContextQuery query,
        OrchestratorRuntime currentRuntime,
        CancellationToken cancellationToken) =>
    {
        if (string.IsNullOrWhiteSpace(query.Owner) || string.IsNullOrWhiteSpace(query.Repo))
        {
            return Results.BadRequest("owner and repo are required.");
        }

        var snapshot = await currentRuntime.GitHubContextService.BuildSnapshotAsync(
            query.ToRepositoryTarget(),
            cancellationToken);

        return Results.Ok(snapshot);
    });

api.MapPost(
    "/speech/token",
    async (OrchestratorRuntime currentRuntime, CancellationToken cancellationToken) =>
    {
        var token = await currentRuntime.SpeechTokenService.GetTokenAsync(cancellationToken);
        return token is null
            ? Results.BadRequest("Azure Speech credentials are not configured.")
            : Results.Ok(token);
    });

app.Run($"http://0.0.0.0:{settings.Port}");
