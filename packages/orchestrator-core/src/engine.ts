import { randomUUID } from "crypto";
import type {
  ConversationInput,
  OrchestrationRun,
  PlanningArtifact,
  ReviewArtifact,
  RunArtifact,
  RunResult,
  RunStatus,
} from "@ai-dev-orchestrator/shared";
import { AnthropicReviewClient } from "./clients/anthropic-client";
import { OpenAiOrchestrationClient } from "./clients/openai-client";
import { GitHubContextService } from "./github/github-context";

function createArtifact(
  stage: RunArtifact["stage"],
  title: string,
  content: string,
  provider?: RunArtifact["provider"],
): RunArtifact {
  return {
    id: randomUUID(),
    stage,
    title,
    content,
    provider,
    createdAt: new Date().toISOString(),
  };
}

export interface StageUpdate {
  status: RunStatus;
  artifact?: RunArtifact;
}

export interface EngineOptions {
  openAiClient?: OpenAiOrchestrationClient;
  anthropicClient?: AnthropicReviewClient;
  githubContextService: GitHubContextService;
}

export class OrchestrationEngine {
  constructor(private readonly options: EngineOptions) {}

  async execute(
    input: ConversationInput,
    onStage?: (update: StageUpdate) => Promise<void> | void,
  ): Promise<RunResult> {
    if (!this.options.openAiClient) {
      throw new Error("OPENAI_API_KEY is required to generate a plan.");
    }

    const context = await this.options.githubContextService.buildSnapshot(input.repository);

    await onStage?.({ status: "planning" });
    const plan = await this.options.openAiClient.createPlan(input.text, context);
    await onStage?.({
      status: "planning",
      artifact: createArtifact(
        "planning",
        "Codex Planning Output",
        formatPlanningArtifact(plan),
        "openai",
      ),
    });

    let review: ReviewArtifact = {
      concerns: [],
      missingContext: [],
      improvements: [],
    };

    if (this.options.anthropicClient) {
      await onStage?.({ status: "reviewing" });
      review = await this.options.anthropicClient.critiquePlan(input.text, plan, context);
      await onStage?.({
        status: "reviewing",
        artifact: createArtifact(
          "reviewing",
          "Claude Review Output",
          formatReviewArtifact(review),
          "anthropic",
        ),
      });
    }

    await onStage?.({ status: "synthesizing" });
    const summary = await this.options.openAiClient.synthesizeBrief(
      input.text,
      plan,
      review,
      context,
    );

    return {
      context,
      plan,
      review,
      summary,
      nextActions: [
        ...plan.firstTasks,
        ...review.improvements.slice(0, 2),
      ].slice(0, 5),
    };
  }
}

export interface RunProcessorDependencies {
  store: {
    get(runId: string): Promise<OrchestrationRun | undefined>;
    update(run: OrchestrationRun): Promise<void>;
  };
  engine: OrchestrationEngine;
}

export class RunProcessor {
  constructor(private readonly dependencies: RunProcessorDependencies) {}

  async process(runId: string): Promise<OrchestrationRun> {
    const run = await this.dependencies.store.get(runId);

    if (!run) {
      throw new Error(`Run '${runId}' was not found.`);
    }

    try {
      const result = await this.dependencies.engine.execute(run.input, async (update) => {
        run.status = update.status;
        run.updatedAt = new Date().toISOString();

        if (update.artifact) {
          run.artifacts.push(update.artifact);
        }

        await this.dependencies.store.update(run);
      });

      run.status = "completed";
      run.updatedAt = new Date().toISOString();
      run.summary = result.summary;
      run.artifacts.push(
        createArtifact("completed", "Final Orchestration Brief", result.summary, "openai"),
      );

      await this.dependencies.store.update(run);
      return run;
    } catch (error) {
      run.status = "failed";
      run.updatedAt = new Date().toISOString();
      run.error = error instanceof Error ? error.message : "Unknown orchestration failure";
      await this.dependencies.store.update(run);
      return run;
    }
  }
}

function formatPlanningArtifact(plan: PlanningArtifact) {
  return [
    `Objective: ${plan.objective}`,
    "",
    "Workstreams:",
    ...plan.workstreams.map((item) => `- ${item}`),
    "",
    "Risks:",
    ...plan.risks.map((item) => `- ${item}`),
    "",
    "First Tasks:",
    ...plan.firstTasks.map((item) => `- ${item}`),
    "",
    `Suggested Branch: ${plan.suggestedBranchName ?? "n/a"}`,
  ].join("\n");
}

function formatReviewArtifact(review: ReviewArtifact) {
  return [
    "Concerns:",
    ...review.concerns.map((item) => `- ${item}`),
    "",
    "Missing Context:",
    ...review.missingContext.map((item) => `- ${item}`),
    "",
    "Improvements:",
    ...review.improvements.map((item) => `- ${item}`),
  ].join("\n");
}
