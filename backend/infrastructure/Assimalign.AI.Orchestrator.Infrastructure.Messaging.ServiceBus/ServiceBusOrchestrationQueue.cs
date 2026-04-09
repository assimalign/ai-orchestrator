using Assimalign.AI.Orchestrator.Core.Models;
using Assimalign.AI.Orchestrator.Infrastructure.Messaging;
using Azure.Messaging.ServiceBus;

namespace Assimalign.AI.Orchestrator.Infrastructure.Messaging.ServiceBus;

public sealed class ServiceBusOrchestrationQueue(string connectionString, string queueName) : IOrchestrationQueue
{
    private readonly ServiceBusClient client = new(connectionString);

    public async Task EnqueueAsync(OrchestrationJob job, CancellationToken cancellationToken = default)
    {
        var sender = client.CreateSender(queueName);

        try
        {
            await sender.SendMessageAsync(
                new ServiceBusMessage(BinaryData.FromObjectAsJson(job)),
                cancellationToken);
        }
        finally
        {
            await sender.DisposeAsync();
        }
    }
}
