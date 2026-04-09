using Assimalign.AI.Orchestrator.Application.Configuration;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;

namespace Assimalign.AI.Orchestrator.Infrastructure.Messaging.ServiceBus;

public static class ServiceBusServiceCollectionExtensions
{
    public static IServiceCollection AddAssimalignAiOrchestratorServiceBusProcessing(
        this IServiceCollection services,
        OrchestratorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrWhiteSpace(settings.ServiceBusConnectionString))
        {
            throw new InvalidOperationException(
                "SERVICE_BUS_CONNECTION_STRING must be configured for background processing.");
        }

        services.AddSingleton(new ServiceBusClient(settings.ServiceBusConnectionString));
        services.AddHostedService<ServiceBusOrchestrationWorker>();
        return services;
    }
}
