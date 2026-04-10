import { useEffect, useRef, useState } from "react";
import type { ThreadMessage } from "../../lib/models";
import { MarkdownContent } from "../ui/MarkdownContent";
import { StatusPill } from "../ui/StatusPill";

export function MessageCard({ message }: { message: ThreadMessage }) {
  const providerLabel =
    message.provider === "codex"
      ? "Codex"
      : message.provider === "claude"
        ? "Claude"
        : message.provider === "github"
          ? "GitHub"
          : message.provider;

  if (message.metadata?.kind === "activity") {
    return <ActivityMessageCard message={message} providerLabel={providerLabel ?? "Activity"} />;
  }

  if (message.role === "stage") {
    return (
      <article className="max-w-4xl rounded-[1.25rem] border border-white/8 bg-white/[0.025] p-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-3">
            <span className="text-[11px] font-semibold uppercase tracking-[0.28em] text-sage-300">
              {providerLabel ?? "Stage"}
            </span>
            <StatusPill status={message.stage ?? "queued"} />
          </div>
          <span className="text-xs text-slate-500">
            {new Date(message.createdAt).toLocaleTimeString()}
          </span>
        </div>
        <h4 className="mt-4 text-sm font-semibold text-white">{message.title}</h4>
        <MarkdownContent
          className="mt-3 break-words text-sm leading-7 text-slate-300"
          content={message.content}
        />
      </article>
    );
  }

  const isUser = message.role === "user";

  return (
    <article
      className={`max-w-4xl rounded-[1.5rem] border p-5 ${
        isUser
          ? "ml-auto border-sage-300/18 bg-sage-300/[0.08]"
          : "border-white/8 bg-white/[0.025]"
      }`}
    >
      <div className="flex flex-wrap items-center justify-between gap-3">
        <span className="text-[11px] font-semibold uppercase tracking-[0.28em] text-sage-300">
          {message.title}
        </span>
        <span className="text-xs text-slate-500">
          {new Date(message.createdAt).toLocaleTimeString()}
        </span>
      </div>
      <MarkdownContent
        className="mt-3 break-words text-sm leading-7 text-slate-200"
        content={message.content}
      />
    </article>
  );
}

function ActivityMessageCard({
  message,
  providerLabel,
}: {
  message: ThreadMessage;
  providerLabel: string;
}) {
  const state = message.metadata?.state ?? "completed";
  const isRunning = state === "running";
  const [expanded, setExpanded] = useState(isRunning);
  const previousRunning = useRef(isRunning);

  useEffect(() => {
    if (isRunning) {
      setExpanded(true);
    } else if (previousRunning.current && !isRunning) {
      setExpanded(false);
    }

    previousRunning.current = isRunning;
  }, [isRunning]);

  return (
    <article
      className={`max-w-4xl rounded-[1.2rem] border p-4 ${
        isRunning
          ? "border-sage-300/15 bg-sage-300/[0.055]"
          : "border-white/8 bg-white/[0.02]"
      }`}
    >
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <span className="text-[11px] font-semibold uppercase tracking-[0.28em] text-sage-300">
            {providerLabel}
          </span>
          {isRunning ? <ThinkingDots /> : null}
        </div>
        <div className="flex items-center gap-2">
          <span className="text-[11px] font-semibold uppercase tracking-[0.24em] text-slate-500">
            {state === "completed" ? "done" : state}
          </span>
          <button
            className="rounded-full border border-white/8 px-3 py-1 text-[11px] font-semibold uppercase tracking-[0.18em] text-slate-300 transition hover:bg-white/[0.05]"
            type="button"
            onClick={() => setExpanded((current) => !current)}
          >
            {expanded ? "Hide" : "Details"}
          </button>
        </div>
      </div>
      <h4 className="mt-3 text-sm font-semibold text-white">{message.title}</h4>
      {expanded ? (
        <MarkdownContent
          className="mt-3 break-words text-sm leading-7 text-slate-300"
          content={message.content}
        />
      ) : (
        <p className="mt-3 text-sm leading-6 text-slate-400">
          {buildCollapsedSummary(message.content)}
        </p>
      )}
    </article>
  );
}

function buildCollapsedSummary(content: string) {
  const normalized = content
    .replace(/```[\s\S]*?```/g, " ")
    .replace(/`/g, "")
    .replace(/\s+/g, " ")
    .trim();

  if (!normalized) {
    return "Step completed.";
  }

  return normalized.length <= 140 ? normalized : `${normalized.slice(0, 137)}...`;
}

function ThinkingDots() {
  return (
    <div className="flex items-center gap-1.5">
      <span className="h-2 w-2 rounded-full bg-sage-300/80 animate-pulse [animation-delay:0ms]" />
      <span className="h-2 w-2 rounded-full bg-sage-300/65 animate-pulse [animation-delay:180ms]" />
      <span className="h-2 w-2 rounded-full bg-sage-300/50 animate-pulse [animation-delay:360ms]" />
    </div>
  );
}
