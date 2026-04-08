import OpenAI from "openai";
import type {
  GitHubContextSnapshot,
  PlanningArtifact,
  ReviewArtifact,
} from "@ai-dev-orchestrator/shared";
import {
  plannerSystemPrompt,
  synthesizerSystemPrompt,
} from "@ai-dev-orchestrator/shared";
import { extractJsonObject } from "../utils/json";

export class OpenAiOrchestrationClient {
  private readonly client: OpenAI;

  constructor(
    apiKey: string,
    private readonly model: string,
  ) {
    this.client = new OpenAI({ apiKey });
  }

  async createPlan(
    requirement: string,
    context?: GitHubContextSnapshot,
  ): Promise<PlanningArtifact> {
    const response = await this.client.responses.create({
      model: this.model,
      reasoning: { effort: "medium" },
      instructions: plannerSystemPrompt,
      input: [
        {
          role: "user",
          content: [
            {
              type: "input_text",
              text: [
                "Requirement:",
                requirement,
                "",
                "GitHub context:",
                JSON.stringify(context ?? {}, null, 2),
              ].join("\n"),
            },
          ],
        },
      ],
    });

    return extractJsonObject<PlanningArtifact>(response.output_text);
  }

  async synthesizeBrief(
    requirement: string,
    plan: PlanningArtifact,
    review: ReviewArtifact,
    context?: GitHubContextSnapshot,
  ): Promise<string> {
    const response = await this.client.responses.create({
      model: this.model,
      reasoning: { effort: "low" },
      instructions: synthesizerSystemPrompt,
      input: [
        {
          role: "user",
          content: [
            {
              type: "input_text",
              text: [
                "Requirement:",
                requirement,
                "",
                "GitHub context:",
                JSON.stringify(context ?? {}, null, 2),
                "",
                "Plan:",
                JSON.stringify(plan, null, 2),
                "",
                "Review:",
                JSON.stringify(review, null, 2),
              ].join("\n"),
            },
          ],
        },
      ],
    });

    return response.output_text.trim();
  }
}
