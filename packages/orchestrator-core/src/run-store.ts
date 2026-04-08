import { TableClient } from "@azure/data-tables";
import type { OrchestrationRun } from "@ai-dev-orchestrator/shared";

export interface RunStore {
  init(): Promise<void>;
  create(run: OrchestrationRun): Promise<void>;
  get(runId: string): Promise<OrchestrationRun | undefined>;
  list(limit?: number): Promise<OrchestrationRun[]>;
  update(run: OrchestrationRun): Promise<void>;
}

export class MemoryRunStore implements RunStore {
  private readonly runs = new Map<string, OrchestrationRun>();

  async init() {}

  async create(run: OrchestrationRun) {
    this.runs.set(run.id, run);
  }

  async get(runId: string) {
    return this.runs.get(runId);
  }

  async list(limit = 20) {
    return [...this.runs.values()]
      .sort((left, right) => right.createdAt.localeCompare(left.createdAt))
      .slice(0, limit);
  }

  async update(run: OrchestrationRun) {
    this.runs.set(run.id, run);
  }
}

export class TableRunStore implements RunStore {
  private readonly client: TableClient;

  constructor(
    connectionString: string,
    tableName: string,
  ) {
    this.client = TableClient.fromConnectionString(connectionString, tableName);
  }

  async init() {
    try {
      await this.client.createTable();
    } catch (error) {
      if ((error as { statusCode?: number }).statusCode !== 409) {
        throw error;
      }
    }
  }

  async create(run: OrchestrationRun) {
    await this.client.upsertEntity(this.toEntity(run));
  }

  async get(runId: string) {
    try {
      const entity = await this.client.getEntity<Record<string, string>>("run", runId);
      return this.fromEntity(entity);
    } catch (error) {
      if ((error as { statusCode?: number }).statusCode === 404) {
        return undefined;
      }

      throw error;
    }
  }

  async list(limit = 20) {
    const runs: OrchestrationRun[] = [];

    for await (const entity of this.client.listEntities<Record<string, string>>({
      queryOptions: { filter: `PartitionKey eq 'run'` },
    })) {
      runs.push(this.fromEntity(entity));
    }

    return runs
      .sort((left, right) => right.createdAt.localeCompare(left.createdAt))
      .slice(0, limit);
  }

  async update(run: OrchestrationRun) {
    await this.client.upsertEntity(this.toEntity(run), "Replace");
  }

  private toEntity(run: OrchestrationRun) {
    return {
      partitionKey: "run",
      rowKey: run.id,
      status: run.status,
      createdAt: run.createdAt,
      updatedAt: run.updatedAt,
      inputJson: JSON.stringify(run.input),
      artifactsJson: JSON.stringify(run.artifacts),
      summary: run.summary ?? "",
      error: run.error ?? "",
    };
  }

  private fromEntity(entity: Record<string, string>): OrchestrationRun {
    return {
      id: entity.rowKey,
      status: entity.status as OrchestrationRun["status"],
      createdAt: entity.createdAt,
      updatedAt: entity.updatedAt,
      input: JSON.parse(entity.inputJson),
      artifacts: JSON.parse(entity.artifactsJson),
      summary: entity.summary || undefined,
      error: entity.error || undefined,
    };
  }
}
