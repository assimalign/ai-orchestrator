import type { ThreadMessage, ThreadStageStatus } from "../../lib/models";
import { StatusPill } from "../ui/StatusPill";

type StageRailProps = {
  activeStatus?: ThreadStageStatus;
  messages: ThreadMessage[];
};

export function StageRail({ activeStatus, messages }: StageRailProps) {
  const liveStage = activeStatus ? getLiveStage(activeStatus) : undefined;

  return (
    <section className="mt-6 rounded-[1.75rem] border border-white/10 bg-white/[0.045] p-5">
      <div className="flex flex-col gap-4 md:flex-row md:items-start md:justify-between">
        <div>
          <p className="text-[11px] font-semibold uppercase tracking-[0.32em] text-sage-300">
            Agent loop
          </p>
          <h3 className="mt-2 text-xl font-semibold text-white">
            Codex and Claude handoff
          </h3>
          <p className="mt-2 max-w-2xl text-sm leading-6 text-slate-400">
            The stage rail keeps the thread readable while still showing the parallel openings, comparison rounds, and final agreement.
          </p>
        </div>
        <StatusPill status={activeStatus ?? "queued"} />
      </div>

      {liveStage ? (
        <div className="mt-5 flex items-center justify-between gap-4 rounded-3xl border border-sage-300/15 bg-sage-300/8 px-4 py-3">
          <div>
            <p className="text-[11px] font-semibold uppercase tracking-[0.28em] text-sage-300">
              {liveStage.provider}
            </p>
            <p className="mt-1 text-sm text-slate-200">{liveStage.message}</p>
          </div>
          <ThinkingDots />
        </div>
      ) : null}

      <div className="mt-5 grid gap-3 xl:grid-cols-4">
        {messages.length ? (
          messages.map((message) => <StageCard key={message.id} message={message} />)
        ) : (
          <>
            <StagePlaceholder
              stage="queued"
              provider="Dispatch"
              description="The thread waits in the queue and keeps its prior conversation intact."
            />
            <StagePlaceholder
              stage="planning"
              provider="Codex + Claude"
              description="Codex and Claude draft their own first-pass responses in parallel."
            />
            <StagePlaceholder
              stage="reviewing"
              provider="Codex + Claude"
              description="Both models compare their responses, discuss reasoning, and push toward alignment."
            />
            <StagePlaceholder
              stage="synthesizing"
              provider="Codex"
              description="Once aligned, Codex turns the agreement into the plan that drives the actual task."
            />
          </>
        )}
      </div>
    </section>
  );
}

function StageCard({ message }: { message: ThreadMessage }) {
  const providerLabel =
    message.provider === "codex"
      ? "Codex"
      : message.provider === "claude"
        ? "Claude"
        : message.provider === "github"
          ? "GitHub"
        : "Agent";

  return (
    <article className="rounded-3xl border border-white/10 bg-black/20 p-4">
      <div className="flex items-start justify-between gap-3">
        <p className="text-[11px] font-semibold uppercase tracking-[0.28em] text-sage-300">
          {providerLabel}
        </p>
        <StatusPill status={message.stage ?? "queued"} />
      </div>
      <p className="mt-4 text-sm font-semibold text-white">{message.title}</p>
      <p className="mt-2 text-sm leading-6 text-slate-400">
        {truncate(message.content, 170)}
      </p>
    </article>
  );
}

function StagePlaceholder({
  description,
  provider,
  stage,
}: {
  description: string;
  provider: string;
  stage: ThreadStageStatus;
}) {
  return (
    <article className="rounded-3xl border border-dashed border-white/10 bg-black/15 p-4">
      <div className="flex items-start justify-between gap-3">
        <p className="text-[11px] font-semibold uppercase tracking-[0.28em] text-sage-300">
          {provider}
        </p>
        <StatusPill status={stage} />
      </div>
      <p className="mt-4 text-sm font-semibold text-white">{labelForStage(stage)}</p>
      <p className="mt-2 text-sm leading-6 text-slate-400">{description}</p>
    </article>
  );
}

function labelForStage(stage: ThreadStageStatus) {
  return (
    {
      queued: "Thread dispatch",
      planning: "Parallel openings",
      reviewing: "Model comparison",
      synthesizing: "Shared agreement",
      completed: "Completed",
      failed: "Failed",
    }[stage] ?? stage
  );
}

function truncate(value: string, limit: number) {
  const normalized = value.replace(/\s+/g, " ").trim();
  return normalized.length <= limit
    ? normalized
    : `${normalized.slice(0, limit - 3)}...`;
}

function getLiveStage(status: ThreadStageStatus) {
  switch (status) {
    case "planning":
      return {
        provider: "Codex + Claude",
        message: "Codex and Claude are drafting their first responses in parallel.",
      };
    case "reviewing":
      return {
        provider: "Codex + Claude",
        message: "Both models are comparing responses, reasoning through differences, and tightening the plan.",
      };
    case "synthesizing":
      return {
        provider: "Codex",
        message: "Codex is turning the shared agreement into the plan that drives the task.",
      };
    default:
      return undefined;
  }
}

function ThinkingDots() {
  return (
    <div className="flex items-center gap-1.5">
      <span className="h-2.5 w-2.5 rounded-full bg-sage-300/80 animate-pulse [animation-delay:0ms]" />
      <span className="h-2.5 w-2.5 rounded-full bg-sage-300/65 animate-pulse [animation-delay:180ms]" />
      <span className="h-2.5 w-2.5 rounded-full bg-sage-300/50 animate-pulse [animation-delay:360ms]" />
    </div>
  );
}
