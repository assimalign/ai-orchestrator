import { randomUUID } from "crypto";
import Fastify from "fastify";
import cors from "@fastify/cors";
import sensible from "@fastify/sensible";
import type { ConversationInput, OrchestrationRun } from "@ai-dev-orchestrator/shared";
import { z } from "zod";
import type { ApiRuntime } from "./runtime";

const createRunSchema = z.object({
  text: z.string().min(1),
  repository: z
    .object({
      owner: z.string().min(1),
      repo: z.string().min(1),
      branch: z.string().optional(),
      issueNumber: z.coerce.number().int().positive().optional(),
      pullRequestNumber: z.coerce.number().int().positive().optional(),
    })
    .optional(),
});

const githubContextQuerySchema = z.object({
  owner: z.string().min(1),
  repo: z.string().min(1),
  branch: z.string().optional(),
  issueNumber: z.coerce.number().int().positive().optional(),
  pullRequestNumber: z.coerce.number().int().positive().optional(),
});

export async function buildApp(runtime: ApiRuntime) {
  const app = Fastify({
    logger: true,
  });

  await app.register(cors, {
    origin: runtime.config.CORS_ORIGIN === "*" ? true : runtime.config.CORS_ORIGIN,
  });
  await app.register(sensible);

  app.get("/healthz", async () => ({
    ok: true,
    mode: runtime.config.EXECUTION_MODE,
  }));

  app.get("/api/config", async () => ({
    executionMode: runtime.config.EXECUTION_MODE,
    speechEnabled: Boolean(runtime.config.AZURE_SPEECH_REGION),
    speechVoice: runtime.config.SPEECH_TTS_VOICE,
    providers: runtime.providerAvailability,
  }));

  app.get("/api/runs", async () => runtime.runStore.list());

  app.get("/api/runs/:runId", async (request, reply) => {
    const params = z.object({ runId: z.string().uuid() }).parse(request.params);
    const run = await runtime.runStore.get(params.runId);

    if (!run) {
      return reply.notFound("Run not found.");
    }

    return run;
  });

  app.post("/api/runs", async (request, reply) => {
    const body = createRunSchema.parse(request.body);

    const run: OrchestrationRun = {
      id: randomUUID(),
      status: "queued",
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      input: body as ConversationInput,
      artifacts: [
        {
          id: randomUUID(),
          stage: "input",
          title: "Requirement Intake",
          content: body.text,
          createdAt: new Date().toISOString(),
        },
      ],
    };

    await runtime.runStore.create(run);

    if (runtime.queue) {
      await runtime.queue.enqueue(run.id);
    } else {
      await runtime.runProcessor.process(run.id);
    }

    const updatedRun = await runtime.runStore.get(run.id);
    return reply.code(202).send(updatedRun);
  });

  app.get("/api/github/repositories", async () =>
    runtime.githubContextService.listRepositories(),
  );

  app.get("/api/github/context", async (request) => {
    const query = githubContextQuerySchema.parse(request.query);
    return runtime.githubContextService.buildSnapshot(query);
  });

  app.post("/api/speech/token", async (request, reply) => {
    if (!runtime.config.AZURE_SPEECH_REGION) {
      return reply.badRequest("AZURE_SPEECH_REGION is not configured.");
    }

    const speechKey = await runtime.secretProvider.get(
      runtime.config.AZURE_SPEECH_KEY_SECRET_NAME,
      runtime.config.AZURE_SPEECH_KEY,
    );

    if (!speechKey) {
      return reply.badRequest("Azure Speech credentials are not configured.");
    }

    const response = await fetch(
      `https://${runtime.config.AZURE_SPEECH_REGION}.api.cognitive.microsoft.com/sts/v1.0/issueToken`,
      {
        method: "POST",
        headers: {
          "Ocp-Apim-Subscription-Key": speechKey,
          "Content-Type": "application/x-www-form-urlencoded",
          "Content-Length": "0",
        },
      },
    );

    if (!response.ok) {
      request.log.error(
        { status: response.status, statusText: response.statusText },
        "Failed to acquire Azure Speech token",
      );
      return reply.badGateway("Unable to acquire Azure Speech token.");
    }

    return {
      token: await response.text(),
      region: runtime.config.AZURE_SPEECH_REGION,
      voice: runtime.config.SPEECH_TTS_VOICE,
    };
  });

  return app;
}
