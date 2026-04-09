using Assimalign.AI.Orchestrator.Application.Runtime;
using Assimalign.AI.Orchestrator.Core.Models;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Assimalign.AI.Orchestrator.Infrastructure.Messaging.ServiceBus;

public sealed class ServiceBusOrchestrationWorker(
    ILogger<ServiceBusOrchestrationWorker> logger,
    ServiceBusClient serviceBusClient,
    OrchestratorRuntime runtime) : BackgroundService
{
    private ServiceBusProcessor? processor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        processor = serviceBusClient.CreateProcessor(runtime.Settings.ServiceBusQueueName);

        processor.ProcessErrorAsync += args =>
        {
            logger.LogError(args.Exception, "Worker subscription error.");
            return Task.CompletedTask;
        };

        processor.ProcessMessageAsync += async args =>
        {
            var job = args.Message.Body.ToObjectFromJson<OrchestrationJob>();

            if (job is null || string.IsNullOrWhiteSpace(job.ThreadId) || string.IsNullOrWhiteSpace(job.MessageId))
            {
                logger.LogWarning("Skipping malformed queue payload: {Payload}", args.Message.Body.ToString());
                await args.CompleteMessageAsync(args.Message, stoppingToken);
                return;
            }

            try
            {
                logger.LogInformation(
                    "Processing thread {ThreadId} from message {MessageId}.",
                    job.ThreadId,
                    job.MessageId);

                await runtime.Processor.ProcessAsync(job.ThreadId, job.MessageId, stoppingToken);
                await args.CompleteMessageAsync(args.Message, stoppingToken);
            }
            catch (Exception error)
            {
                logger.LogError(
                    error,
                    "Failed processing thread {ThreadId} from queue message {MessageId}.",
                    job.ThreadId,
                    job.MessageId);
                await args.AbandonMessageAsync(args.Message, cancellationToken: stoppingToken);
            }
        };

        await processor.StartProcessingAsync(stoppingToken);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (processor is not null)
        {
            await processor.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}
