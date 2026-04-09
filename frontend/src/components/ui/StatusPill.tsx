import type { ThreadStageStatus } from "../../lib/models";

const toneClasses: Record<ThreadStageStatus, string> = {
  queued: "border-white/10 bg-white/10 text-slate-200",
  planning: "border-amber-400/20 bg-amber-400/10 text-amber-100",
  reviewing: "border-sky-400/20 bg-sky-400/10 text-sky-100",
  synthesizing: "border-sage-400/20 bg-sage-400/10 text-sage-100",
  completed: "border-emerald-400/20 bg-emerald-400/10 text-emerald-100",
  failed: "border-rose-400/20 bg-rose-400/10 text-rose-100",
};

export function StatusPill({ status }: { status: ThreadStageStatus }) {
  const label =
    {
      queued: "queued",
      planning: "opening",
      reviewing: "comparing",
      synthesizing: "agreeing",
      completed: "ready",
      failed: "failed",
    }[status] ?? status;

  return (
    <span
      className={`rounded-full border px-2.5 py-1 text-[10px] font-semibold uppercase tracking-[0.24em] ${toneClasses[status]}`}
    >
      {label}
    </span>
  );
}
