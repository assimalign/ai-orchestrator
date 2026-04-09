import type { ConversationThread } from "../../lib/models";
import { StatusPill } from "../ui/StatusPill";

type ThreadSidebarProps = {
  accountLabel?: string;
  activeThreadId?: string;
  onSelectThread: (threadId: string) => void;
  onSignOut: () => Promise<void>;
  onStartNewThread: () => void;
  threads: ConversationThread[];
};

export function ThreadSidebar({
  accountLabel,
  activeThreadId,
  onSelectThread,
  onSignOut,
  onStartNewThread,
  threads,
}: ThreadSidebarProps) {
  return (
    <aside className="flex h-full min-h-0 flex-col overflow-hidden rounded-[2rem] border border-white/10 bg-black/20 p-4 backdrop-blur">
      <div className="flex items-start justify-between gap-4">
        <div>
          <p className="text-[11px] font-semibold uppercase tracking-[0.32em] text-sage-300">
            Threads
          </p>
          <h2 className="mt-2 text-2xl font-semibold tracking-tight text-white">
            Orchestrator
          </h2>
        </div>

        <button
          className="rounded-full border border-white/10 bg-white/5 px-4 py-2 text-sm font-medium text-slate-100 transition hover:bg-white/10"
          type="button"
          onClick={onStartNewThread}
        >
          New
        </button>
      </div>

      <div className="mt-6 flex-1 space-y-3 overflow-y-auto pr-1">
        {threads.length === 0 ? (
          <div className="rounded-3xl border border-dashed border-white/10 bg-white/5 p-4 text-sm text-slate-400">
            <p className="font-medium text-white">No threads yet.</p>
            <p className="mt-2 leading-6">
              Start a conversation and the workspace will keep Codex and Claude activity in one place.
            </p>
          </div>
        ) : (
          threads.map((thread) => {
            const active = thread.id === activeThreadId;

            return (
              <button
                key={thread.id}
                className={`w-full rounded-3xl border p-4 text-left transition ${
                  active
                    ? "border-sage-300/30 bg-sage-300/10 shadow-[inset_0_1px_0_rgba(255,255,255,0.04)]"
                    : "border-white/10 bg-white/5 hover:bg-white/10"
                }`}
                type="button"
                onClick={() => onSelectThread(thread.id)}
              >
                <div className="flex items-start justify-between gap-3">
                  <strong className="line-clamp-2 text-sm font-semibold text-white">
                    {thread.title}
                  </strong>
                  <StatusPill status={thread.status} />
                </div>
                <p className="mt-3 line-clamp-3 text-sm leading-6 text-slate-400">
                  {thread.lastMessagePreview || "Waiting for activity."}
                </p>
              </button>
            );
          })
        )}
      </div>

      <div className="mt-4 rounded-3xl border border-white/10 bg-white/5 p-4">
        <p className="text-[11px] font-semibold uppercase tracking-[0.3em] text-sage-300">
          Signed in
        </p>
        <p className="mt-2 text-sm font-medium text-white">
          {accountLabel ?? "Microsoft account"}
        </p>
        <button
          className="mt-4 w-full rounded-full border border-white/10 bg-transparent px-4 py-3 text-sm font-medium text-slate-100 transition hover:bg-white/10"
          type="button"
          onClick={() => void onSignOut()}
        >
          Sign out
        </button>
      </div>
    </aside>
  );
}
