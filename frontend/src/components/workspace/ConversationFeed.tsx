import type { ConversationThreadDetail } from "../../lib/models";
import { MessageCard } from "./MessageCard";

export function ConversationFeed({
  threadDetail,
}: {
  threadDetail?: ConversationThreadDetail;
}) {
  const liveMessage = threadDetail ? getLiveMessage(threadDetail.thread.status) : undefined;

  return (
    <div className="min-h-[22rem] max-h-[min(42rem,55vh)] flex-1 space-y-4 overflow-y-auto pb-5 pr-2">
      {threadDetail?.messages?.length ? (
        <>
          {threadDetail.messages.map((message) => (
            <MessageCard key={message.id} message={message} />
          ))}
          {liveMessage ? <ThinkingMessageCard {...liveMessage} /> : null}
        </>
      ) : (
        <div className="rounded-[1.75rem] border border-white/10 bg-white/[0.045] p-6">
          <p className="text-[11px] font-semibold uppercase tracking-[0.32em] text-sage-300">
            Ready
          </p>
          <h3 className="mt-3 text-2xl font-semibold text-white">
            Keep working inside one thread.
          </h3>
          <p className="mt-4 max-w-2xl text-sm leading-7 text-slate-400">
            Ask for a feature, a bug fix, or a design review. Codex will plan,
            Claude will critique, and the thread will keep the stages visible without flooding the timeline.
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
        provider: "Codex",
        title: "Thinking through the first pass",
        content: "Codex is working through the request and shaping the opening response.",
      };
    case "reviewing":
      return {
        provider: "Claude",
        title: "Reviewing the approach",
        content: "Claude is pressure-testing the plan before the answer comes back.",
      };
    case "synthesizing":
      return {
        provider: "Codex",
        title: "Pulling the answer together",
        content: "Codex is folding the back-and-forth into the response you’ll see here.",
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
    <article className="max-w-4xl rounded-[1.5rem] border border-sage-300/20 bg-sage-300/6 p-5">
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
