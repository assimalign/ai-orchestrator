import Anthropic from "@anthropic-ai/sdk";
import type {
  GitHubContextSnapshot,
  PlanningArtifact,
  ReviewArtifact,
} from "@ai-dev-orchestrator/shared";
import { reviewerSystemPrompt } from "@ai-dev-orchestrator/shared";
import { extractJsonObject } from "../utils/json";

export class AnthropicReviewClient {
  private readonly client: Anthropic;

  constructor(
    apiKey: string,
    private readonly model: string,
  ) {
    this.client = new Anthropic({ apiKey });
  }

  async critiquePlan(
    requirement: string,
    plan: PlanningArtifact,
    context?: GitHubContextSnapshot,
  ): Promise<ReviewArtifact> {
    const response = await this.client.messages.create({
      model: this.model,
      max_tokens: 1200,
      system: reviewerSystemPrompt,
      messages: [
        {
          role: "user",
          content: [
            {
              type: "text",
              text: [
                "Requirement:",
                requirement,
                "",
                "GitHub context:",
                JSON.stringify(context ?? {}, null, 2),
                "",
                "Plan:",
                JSON.stringify(plan, null, 2),
              ].join("\n"),
            },
          ],
        },
      ],
    });

    const content = response.content
      .filter((item) => item.type === "text")
      .map((item) => item.text)
      .join("\n");

    return extractJsonObject<ReviewArtifact>(content);
  }
}
