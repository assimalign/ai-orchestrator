import type { ConversationThreadDetail } from "../../lib/models";
import { MessageCard } from "./MessageCard";

export function ConversationFeed({
  threadDetail,
}: {
  threadDetail?: ConversationThreadDetail;
}) {
  const liveMessage = threadDetail ? getLiveMessage(threadDetail.thread.status) : undefined;

  return (
    <div className="min-h-[28rem] flex-1 space-y-4 overflow-y-auto pb-6 pr-2">
      {threadDetail?.messages?.length ? (
        <>
          {threadDetail.messages.map((message) => (
            <MessageCard key={message.id} message={message} />
          ))}
          {liveMessage ? <ThinkingMessageCard {...liveMessage} /> : null}
        </>
      ) : (
        <div className="rounded-[1.5rem] border border-white/8 bg-white/[0.025] p-6">
          <p className="text-[11px] font-semibold uppercase tracking-[0.32em] text-sage-300">
            Ready
          </p>
          <h3 className="mt-3 text-2xl font-semibold text-white">
            Keep everything in one thread.
          </h3>
          <p className="mt-4 max-w-2xl text-sm leading-7 text-slate-400">
            Ask for a feature, a bug fix, or a repo review. The thread keeps the model discussion, execution steps, and final result in one clean stream.
          </p>
        </div>
      )}
    </div>
  );
}

function getLiveMessage(status: ConversationThreadDetail["thread"]["status"]) {
  switch (status) {
    case "planning":
      return {
        provider: "Codex + Claude",
        title: "Drafting first responses",
        content: "Codex and Claude are each preparing their own first take before they compare notes.",
      };
    case "reviewing":
      return {
        provider: "Codex + Claude",
        title: "Comparing approaches",
        content: "Both models are discussing their reasoning, reconciling differences, and pushing toward one direction.",
      };
    case "synthesizing":
      return {
        provider: "Codex",
        title: "Turning agreement into action",
        content: "Codex is converting the shared agreement into the response and execution plan you’ll see here.",
      };
    default:
      return undefined;
  }
}

function ThinkingMessageCard({
  content,
  provider,
  title,
}: {
  content: string;
  provider: string;
  title: string;
}) {
  return (
    <article className="max-w-4xl rounded-[1.35rem] border border-sage-300/18 bg-sage-300/[0.06] p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <span className="text-[11px] font-semibold uppercase tracking-[0.28em] text-sage-300">
          {provider}
        </span>
        <ThinkingDots />
      </div>
      <h4 className="mt-4 text-sm font-semibold text-white">{title}</h4>
      <div className="mt-3 text-sm leading-7 text-slate-300">{content}</div>
    </article>
  );
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
