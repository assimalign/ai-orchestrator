import type {
  ConnectorDefinition,
  ConnectorStatusResponse,
} from "../../lib/models";

type ConnectorManagementModalProps = {
  connectors: ConnectorDefinition[];
  isLoading: boolean;
  onClose: () => void;
  onRefresh: () => Promise<void>;
  statuses: Record<string, ConnectorStatusResponse>;
};

export function ConnectorManagementModal({
  connectors,
  isLoading,
  onClose,
  onRefresh,
  statuses,
}: ConnectorManagementModalProps) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-[#020304]/80 p-4 backdrop-blur-sm">
      <div className="flex max-h-[88vh] w-full max-w-4xl flex-col overflow-hidden rounded-[2rem] border border-white/10 bg-[#101214] shadow-[0_30px_90px_rgba(0,0,0,0.55)]">
        <div className="flex items-start justify-between gap-4 border-b border-white/8 px-6 py-5">
          <div>
            <p className="text-[11px] font-semibold uppercase tracking-[0.32em] text-sage-300">
              Connector management
            </p>
            <h2 className="mt-2 text-2xl font-semibold text-white">
              Repository connectors
            </h2>
            <p className="mt-2 max-w-2xl text-sm leading-6 text-slate-400">
              Connectors tell Codex and Claude which repositories they can inspect, branch, and push against.
              Provisioning secrets through Bicep does not remove the runtime GitHub credential requirement.
            </p>
          </div>

          <button
            className="inline-flex h-11 w-11 items-center justify-center rounded-full border border-white/10 bg-white/[0.04] text-slate-300 transition hover:bg-white/[0.08]"
            type="button"
            aria-label="Close connector management"
            onClick={onClose}
          >
            <CloseIcon />
          </button>
        </div>

        <div className="overflow-y-auto px-6 py-5">
          <div className="grid gap-4 xl:grid-cols-2">
            {connectors.map((connector) => {
              const status = statuses[connector.id];
              const statusLabel = resolveStatusLabel(status, connector);
              const statusTone = resolveStatusTone(status, connector);

              return (
                <article
                  key={connector.id}
                  className="rounded-[1.6rem] border border-white/10 bg-white/[0.03] p-5"
                >
                  <div className="flex items-start justify-between gap-4">
                    <div>
                      <p className="text-lg font-semibold text-white">{connector.label}</p>
                      <p className="mt-2 text-sm leading-6 text-slate-400">
                        {connector.description}
                      </p>
                    </div>
                    <span className={`rounded-full px-3 py-1 text-[11px] font-semibold uppercase tracking-[0.22em] ${statusTone}`}>
                      {statusLabel}
                    </span>
                  </div>

                  <div className="mt-4 flex flex-wrap gap-2">
                    <MetaChip label={status?.authMode || connector.authMode || "Not configured"} />
                    <MetaChip
                      label={
                        status
                          ? `${status.repositoryCount} repos visible`
                          : connector.enabled
                            ? "Runtime check pending"
                            : "Credentials missing"
                      }
                    />
                  </div>

                  <p className="mt-4 rounded-[1.2rem] border border-white/8 bg-black/20 px-4 py-3 text-sm leading-6 text-slate-300">
                    {status?.message || connector.setupSummary}
                  </p>

                  <div className="mt-4">
                    <p className="text-[11px] font-semibold uppercase tracking-[0.28em] text-slate-500">
                      Capabilities
                    </p>
                    <ul className="mt-3 space-y-2 text-sm text-slate-300">
                      {connector.capabilities.map((capability) => (
                        <li key={capability} className="flex items-start gap-2">
                          <span className="mt-1 h-1.5 w-1.5 rounded-full bg-sage-300" />
                          <span>{capability}</span>
                        </li>
                      ))}
                    </ul>
                  </div>
                </article>
              );
            })}
          </div>

          <div className="mt-5 rounded-[1.4rem] border border-amber-300/15 bg-amber-300/8 px-5 py-4 text-sm leading-6 text-amber-100">
            For private repos and any write operation like creating branches, committing, or pushing, the orchestrator
            still needs runtime GitHub credentials. You can use either a GitHub App or a runtime token, but not neither.
          </div>
        </div>

        <div className="flex flex-col gap-3 border-t border-white/8 px-6 py-4 sm:flex-row sm:items-center sm:justify-between">
          <p className="text-xs text-slate-500">
            {isLoading
              ? "Refreshing runtime connector status..."
              : "Use refresh after redeploying connector secrets or changing GitHub access."}
          </p>
          <div className="flex gap-3">
            <button
              className="rounded-full border border-white/10 bg-white/[0.04] px-4 py-2.5 text-sm font-medium text-slate-200 transition hover:bg-white/[0.08]"
              type="button"
              onClick={() => void onRefresh()}
              disabled={isLoading}
            >
              {isLoading ? "Refreshing..." : "Refresh status"}
            </button>
            <button
              className="rounded-full bg-sage-300 px-4 py-2.5 text-sm font-semibold text-ink-950 transition hover:bg-sage-200"
              type="button"
              onClick={onClose}
            >
              Done
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

function MetaChip({ label }: { label: string }) {
  return (
    <span className="rounded-full border border-white/10 bg-white/[0.04] px-3 py-1.5 text-xs font-medium text-slate-300">
      {label}
    </span>
  );
}

function resolveStatusLabel(
  status: ConnectorStatusResponse | undefined,
  connector: ConnectorDefinition,
) {
  switch (status?.status) {
    case "ready":
      return "Ready";
    case "error":
      return "Error";
    case "configurationRequired":
      return "Needs setup";
    default:
      return connector.enabled ? "Checking" : "Unavailable";
  }
}

function resolveStatusTone(
  status: ConnectorStatusResponse | undefined,
  connector: ConnectorDefinition,
) {
  switch (status?.status) {
    case "ready":
      return "bg-sage-300/15 text-sage-200";
    case "error":
      return "bg-rose-400/15 text-rose-200";
    case "configurationRequired":
      return "bg-amber-300/15 text-amber-100";
    default:
      return connector.enabled
        ? "bg-sky-400/15 text-sky-200"
        : "bg-slate-400/15 text-slate-300";
  }
}

function CloseIcon() {
  return (
    <svg
      aria-hidden="true"
      className="h-4 w-4"
      fill="none"
      viewBox="0 0 24 24"
      stroke="currentColor"
      strokeWidth="1.8"
    >
      <path strokeLinecap="round" strokeLinejoin="round" d="M6 6l12 12M18 6 6 18" />
    </svg>
  );
}
