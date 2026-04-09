import type { ConversationThreadDetail } from "../../lib/models";
import { MessageCard } from "./MessageCard";

export function ConversationFeed({
  threadDetail,
}: {
  threadDetail?: ConversationThreadDetail;
}) {
  return (
    <div className="min-h-[22rem] max-h-[min(42rem,55vh)] flex-1 space-y-4 overflow-y-auto pb-5 pr-2">
      {threadDetail?.messages?.length ? (
        threadDetail.messages.map((message) => (
          <MessageCard key={message.id} message={message} />
        ))
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
