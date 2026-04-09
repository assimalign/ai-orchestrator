import type { AppConfigResponse, ConversationThread } from "../../lib/models";

type WorkspaceHeaderProps = {
  activeThread?: ConversationThread;
  config?: AppConfigResponse;
  statusMessage?: string;
};

function MetricCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-3xl border border-white/10 bg-white/5 px-4 py-3">
      <p className="text-[11px] font-semibold uppercase tracking-[0.28em] text-sage-300">
        {label}
      </p>
      <p className="mt-2 text-sm font-medium text-white">{value}</p>
    </div>
  );
}

export function WorkspaceHeader({ activeThread, config, statusMessage }: WorkspaceHeaderProps) {
  return (
    <header className="flex flex-col gap-5 lg:flex-row lg:items-start lg:justify-between">
      <div>
        <p className="text-[11px] font-semibold uppercase tracking-[0.32em] text-sage-300">
          Codex-style orchestration
        </p>
        <h1 className="mt-3 text-3xl font-semibold tracking-tight text-white">
          {activeThread?.title ?? "Start a new thread"}
        </h1>
        <p className="mt-3 max-w-2xl text-sm leading-7 text-slate-400">
          Persistent conversations, repository-bound working branches, and one protected place to coordinate Codex and Claude before promoting upstream.
        </p>
        {!config && statusMessage ? (
          <div className="mt-4 max-w-2xl rounded-2xl border border-rose-400/20 bg-rose-500/10 px-4 py-3 text-sm text-rose-100">
            {statusMessage}
          </div>
        ) : null}
      </div>

      <div className="grid gap-3 sm:grid-cols-3">
        <MetricCard label="Mode" value={config?.executionMode ?? "loading"} />
        <MetricCard label="Speech" value={config?.speechEnabled ? "ready" : "off"} />
        <MetricCard
          label="Providers"
          value={`${config?.providers.openAi ? "Codex" : "Codex off"} / ${config?.providers.anthropic ? "Claude" : "Claude off"}`}
        />
      </div>
    </header>
  );
}
