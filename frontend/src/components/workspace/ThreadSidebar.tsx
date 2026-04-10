import type { ConversationThread } from "../../lib/models";
import { StatusPill } from "../ui/StatusPill";

type ThreadSidebarProps = {
  accountLabel?: string;
  activeThreadId?: string;
  deletingThreadId?: string;
  onDeleteThread: (threadId: string) => void;
  onSelectThread: (threadId: string) => void;
  onSignOut: () => Promise<void>;
  onStartNewThread: () => void;
  threads: ConversationThread[];
};

export function ThreadSidebar({
  accountLabel,
  activeThreadId,
  deletingThreadId,
  onDeleteThread,
  onSelectThread,
  onSignOut,
  onStartNewThread,
  threads,
}: ThreadSidebarProps) {
  return (
    <aside className="flex min-h-[calc(100vh-2.5rem)] min-w-0 flex-col overflow-hidden rounded-[2rem] border border-white/8 bg-[linear-gradient(180deg,rgba(148,199,176,0.035),rgba(255,255,255,0.015))] p-4 shadow-[0_18px_70px_rgba(0,0,0,0.28)] backdrop-blur xl:sticky xl:top-5 xl:max-h-[calc(100vh-2.5rem)]">
      <div className="flex items-start justify-between gap-4">
        <div>
          <p className="text-[11px] font-semibold uppercase tracking-[0.32em] text-sage-300">
            Threads
          </p>
          <h2 className="mt-2 text-[1.9rem] font-semibold tracking-tight text-white">
            Orchestrator
          </h2>
        </div>

        <button
          className="rounded-full border border-white/8 bg-white/[0.04] px-4 py-2 text-sm font-medium text-slate-100 transition hover:bg-white/[0.08]"
          type="button"
          onClick={onStartNewThread}
        >
          New
        </button>
      </div>

      <div className="mt-6 flex-1 space-y-3 overflow-y-auto pr-1">
        {threads.length === 0 ? (
          <div className="rounded-[1.4rem] border border-dashed border-white/8 bg-white/[0.03] p-4 text-sm text-slate-400">
            <p className="font-medium text-white">No threads yet.</p>
            <p className="mt-2 leading-6">
              Start a conversation and the workspace will keep Codex and Claude activity in one place.
            </p>
          </div>
        ) : (
          threads.map((thread) => {
            const active = thread.id === activeThreadId;
            const isDeleting = deletingThreadId === thread.id;

            return (
              <article
                key={thread.id}
                className={`rounded-3xl border p-4 transition ${
                  active
                    ? "border-sage-300/20 bg-sage-300/[0.08] shadow-[inset_0_1px_0_rgba(255,255,255,0.04)]"
                    : "border-white/8 bg-white/[0.03] hover:bg-white/[0.055]"
                }`}
              >
                <div className="flex items-start gap-3">
                  <button
                    className="min-w-0 flex-1 text-left"
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
                  <button
                    aria-label={`Delete ${thread.title}`}
                    className="rounded-full border border-white/8 bg-black/10 px-2.5 py-1 text-xs font-semibold text-slate-300 transition hover:border-rose-300/30 hover:bg-rose-400/10 hover:text-rose-100 disabled:cursor-not-allowed disabled:opacity-50"
                    disabled={isDeleting}
                    type="button"
                    onClick={() => onDeleteThread(thread.id)}
                  >
                    {isDeleting ? "..." : "×"}
                  </button>
                </div>
              </article>
            );
          })
        )}
      </div>

      <div className="mt-4 rounded-[1.4rem] border border-white/8 bg-white/[0.03] p-4">
        <p className="text-[11px] font-semibold uppercase tracking-[0.3em] text-sage-300">
          Signed in
        </p>
        <p className="mt-2 text-sm font-medium text-white">
          {accountLabel ?? "Microsoft account"}
        </p>
        <button
          className="mt-4 w-full rounded-full border border-white/8 bg-transparent px-4 py-3 text-sm font-medium text-slate-100 transition hover:bg-white/[0.08]"
          type="button"
          onClick={() => void onSignOut()}
        >
          Sign out
        </button>
      </div>
    </aside>
  );
}
