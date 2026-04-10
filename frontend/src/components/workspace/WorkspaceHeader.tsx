import type { AppConfigResponse, ConversationThread } from "../../lib/models";

type WorkspaceHeaderProps = {
  activeThread?: ConversationThread;
  config?: AppConfigResponse;
  statusMessage?: string;
};

function MetricChip({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-full border border-white/8 bg-white/[0.03] px-3.5 py-2.5">
      <p className="text-[10px] font-semibold uppercase tracking-[0.22em] text-slate-500">
        {label}
      </p>
      <p className="mt-1 text-sm font-medium text-white/95">{value}</p>
    </div>
  );
}

export function WorkspaceHeader({ activeThread, config, statusMessage }: WorkspaceHeaderProps) {
  return (
    <header className="flex flex-col gap-5 border-b border-white/8 pb-6 lg:flex-row lg:items-end lg:justify-between">
      <div className="max-w-3xl">
        <p className="text-[11px] font-semibold uppercase tracking-[0.28em] text-sage-300">
          Assimalign AI Orchestrator
        </p>
        <h1 className="mt-3 text-3xl font-semibold tracking-tight text-white sm:text-[2.2rem]">
          {activeThread?.title ?? "Start a new thread"}
        </h1>
        <p className="mt-3 text-sm leading-7 text-slate-400">
          A focused workspace for repository work, model debate, and one clean thread of execution.
        </p>
        {!config && statusMessage ? (
          <div className="mt-4 rounded-2xl border border-rose-400/20 bg-rose-500/10 px-4 py-3 text-sm text-rose-100">
            {statusMessage}
          </div>
        ) : null}
      </div>

      <div className="flex flex-wrap gap-2.5">
        <MetricChip label="Mode" value={config?.executionMode ?? "loading"} />
        <MetricChip label="Speech" value={config?.speechEnabled ? "ready" : "off"} />
        <MetricChip
          label="Providers"
          value={`${config?.providers.openAi ? "Codex" : "Codex off"} / ${config?.providers.anthropic ? "Claude" : "Claude off"}`}
        />
      </div>
    </header>
  );
}
