using Assimalign.AI.Orchestrator.Application.Configuration;
using Assimalign.AI.Orchestrator.Application.Hosting;
using Assimalign.AI.Orchestrator.Infrastructure.Composition;
using Assimalign.AI.Orchestrator.Infrastructure.Messaging.ServiceBus;

var builder = Host.CreateApplicationBuilder(args);
var settings = OrchestratorSettings.Load(builder.Configuration);

if (!settings.UsesServiceBus || string.IsNullOrWhiteSpace(settings.ServiceBusConnectionString))
{
    throw new InvalidOperationException(
        "The worker requires EXECUTION_MODE=servicebus and SERVICE_BUS_CONNECTION_STRING.");
}

builder.Services.AddAssimalignAiOrchestratorTelemetry(
    builder.Configuration,
    "Assimalign.AI.Orchestrator.Worker");
builder.Services.AddAssimalignAiOrchestratorRuntime(settings);
builder.Services.AddAssimalignAiOrchestratorServiceBusProcessing(settings);

var host = builder.Build();
await host.RunAsync();
