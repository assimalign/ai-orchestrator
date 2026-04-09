import type { ThreadMessage } from "../../lib/models";
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

  if (message.role === "stage") {
    return (
      <article className="max-w-4xl rounded-[1.5rem] border border-sage-300/15 bg-ink-800/80 p-5">
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
        <div className="mt-3 whitespace-pre-wrap break-words text-sm leading-7 text-slate-300">
          {message.content}
        </div>
      </article>
    );
  }

  const isUser = message.role === "user";

  return (
    <article
      className={`max-w-4xl rounded-[1.5rem] border p-5 ${
        isUser
          ? "ml-auto border-sage-300/20 bg-sage-300/10"
          : "border-white/10 bg-white/[0.045]"
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
      <div className="mt-3 whitespace-pre-wrap break-words text-sm leading-7 text-slate-200">
        {message.content}
      </div>
    </article>
  );
}
