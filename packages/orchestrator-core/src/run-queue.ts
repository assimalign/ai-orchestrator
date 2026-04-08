import { ServiceBusClient } from "@azure/service-bus";

export interface RunQueue {
  enqueue(runId: string): Promise<void>;
}

export class ServiceBusRunQueue implements RunQueue {
  private readonly client: ServiceBusClient;

  constructor(
    connectionString: string,
    private readonly queueName: string,
  ) {
    this.client = new ServiceBusClient(connectionString);
  }

  async enqueue(runId: string) {
    const sender = this.client.createSender(this.queueName);
    try {
      await sender.sendMessages({
        body: {
          runId,
        },
      });
    } finally {
      await sender.close();
    }
  }
}
