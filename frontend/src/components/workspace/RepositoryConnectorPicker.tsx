import { useEffect, useMemo, useRef, useState } from "react";
import type {
  ConnectorDefinition,
  ConnectorRepositoryReference,
} from "../../lib/models";

type RepositoryConnectorPickerProps = {
  connectorId: string;
  connectors: ConnectorDefinition[];
  isLoading: boolean;
  onConnectorChange: (connectorId: string) => void;
  onManageConnectors: () => void;
  onRepositorySelect: (repository: ConnectorRepositoryReference) => void;
  repositories: ConnectorRepositoryReference[];
  selectedOwner: string;
  selectedRepo: string;
};

export function RepositoryConnectorPicker({
  connectorId,
  connectors,
  isLoading,
  onConnectorChange,
  onManageConnectors,
  onRepositorySelect,
  repositories,
  selectedOwner,
  selectedRepo,
}: RepositoryConnectorPickerProps) {
  const [isOpen, setIsOpen] = useState(false);
  const [query, setQuery] = useState("");
  const rootRef = useRef<HTMLDivElement | null>(null);
  const selectedFullName = selectedOwner && selectedRepo ? `${selectedOwner}/${selectedRepo}` : "";
  const activeConnector = connectors.find((connector) => connector.id === connectorId);

  const filteredRepositories = useMemo(() => {
    const normalizedQuery = query.trim().toLowerCase();
    if (!normalizedQuery) {
      return repositories;
    }

    return repositories.filter((repository) =>
      [
        repository.owner,
        repository.repo,
        `${repository.owner}/${repository.repo}`,
        repository.description,
      ]
        .filter(Boolean)
        .some((value) => value.toLowerCase().includes(normalizedQuery)));
  }, [query, repositories]);

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    function handlePointerDown(event: MouseEvent) {
      if (rootRef.current?.contains(event.target as Node)) {
        return;
      }

      setIsOpen(false);
    }

    document.addEventListener("mousedown", handlePointerDown);
    return () => document.removeEventListener("mousedown", handlePointerDown);
  }, [isOpen]);

  return (
    <div ref={rootRef} className="relative">
      <button
        className="flex w-full cursor-pointer items-center justify-between gap-3 rounded-[1.2rem] border border-white/10 bg-white/[0.04] px-3.5 py-3 text-left transition hover:bg-white/[0.06]"
        type="button"
        onClick={() => setIsOpen((current) => !current)}
      >
        <div className="min-w-0">
          <span className="text-[10px] font-semibold uppercase tracking-[0.26em] text-slate-500">
            Repository
          </span>
          <div className="mt-2 flex min-w-0 items-center gap-2">
            <span className="rounded-full border border-white/10 bg-white/5 px-2.5 py-1 text-[11px] font-semibold uppercase tracking-[0.18em] text-sage-300">
              {activeConnector?.label ?? "Connector"}
            </span>
            <span className="truncate text-sm text-slate-100">
              {selectedFullName || "Search environments and repos..."}
            </span>
          </div>
        </div>
        <span className="shrink-0 text-slate-500">
          <ChevronDownIcon />
        </span>
      </button>

      {isOpen ? (
        <div className="absolute left-0 z-40 mt-2 w-full min-w-[22rem] overflow-hidden rounded-[1.4rem] border border-white/10 bg-[#232325] shadow-[0_20px_60px_rgba(0,0,0,0.42)]">
          <div className="border-b border-white/8 p-3">
            <input
              autoFocus
              className="w-full rounded-2xl border border-white/10 bg-[#2d2d30] px-3.5 py-2.5 text-sm text-slate-100 outline-none placeholder:text-slate-500"
              placeholder="Search environments and repos..."
              value={query}
              onChange={(event) => setQuery(event.target.value)}
            />
          </div>

          <div className="max-h-[24rem] overflow-y-auto p-3">
            <p className="px-1 text-[11px] font-semibold uppercase tracking-[0.26em] text-slate-500">
              Connectors
            </p>
            <div className="mt-2 space-y-1.5">
              {connectors.map((connector) => (
                <button
                  key={connector.id}
                  className={`flex w-full cursor-pointer items-center justify-between rounded-2xl px-3 py-2 text-left transition ${
                    connector.id === connectorId
                      ? "bg-white/[0.08] text-white"
                      : "text-slate-300 hover:bg-white/[0.05]"
                  }`}
                  type="button"
                  onClick={() => onConnectorChange(connector.id)}
                >
                  <div>
                    <p className="text-sm font-medium">{connector.label}</p>
                    <p className="mt-1 text-xs text-slate-500">{connector.description}</p>
                  </div>
                  <span
                    className={`rounded-full px-2 py-1 text-[10px] font-semibold uppercase tracking-[0.2em] ${
                      connector.enabled
                        ? "bg-sage-300/15 text-sage-200"
                        : "bg-rose-400/10 text-rose-200"
                    }`}
                  >
                    {connector.enabled ? "Ready" : "Off"}
                  </span>
                </button>
              ))}
            </div>

            <p className="mt-5 px-1 text-[11px] font-semibold uppercase tracking-[0.26em] text-slate-500">
              Repositories
            </p>
            <div className="mt-2 space-y-1.5">
              {isLoading ? (
                <div className="rounded-2xl border border-white/10 bg-white/[0.03] px-3 py-4 text-sm text-slate-400">
                  Loading repositories...
                </div>
              ) : filteredRepositories.length > 0 ? (
                filteredRepositories.map((repository) => {
                  const fullName = `${repository.owner}/${repository.repo}`;
                  const selected = fullName === selectedFullName;

                  return (
                    <button
                      key={`${repository.connectorId}:${fullName}`}
                      className={`w-full cursor-pointer rounded-2xl border px-3 py-3 text-left transition ${
                        selected
                          ? "border-sage-300/25 bg-sage-300/10"
                          : "border-white/10 bg-white/[0.03] hover:bg-white/[0.05]"
                      }`}
                      type="button"
                      onClick={() => {
                        onRepositorySelect(repository);
                        setIsOpen(false);
                        setQuery("");
                      }}
                    >
                      <div className="flex items-start justify-between gap-3">
                        <div className="min-w-0">
                          <p className="truncate text-sm font-medium text-white">{fullName}</p>
                          <p className="mt-1 truncate text-xs text-slate-500">
                            {repository.description || `Default branch ${repository.defaultBranch || "main"}`}
                          </p>
                        </div>
                        {selected ? (
                          <span className="rounded-full bg-sage-300/15 px-2 py-1 text-[10px] font-semibold uppercase tracking-[0.2em] text-sage-200">
                            Selected
                          </span>
                        ) : null}
                      </div>
                    </button>
                  );
                })
              ) : (
                <div className="rounded-2xl border border-white/10 bg-white/[0.03] px-3 py-4 text-sm text-slate-400">
                  {activeConnector?.enabled
                    ? "No repositories matched your search."
                    : "This connector is not configured yet."}
                </div>
              )}
            </div>
          </div>

          <div className="border-t border-white/8 p-3">
            <button
              className="w-full cursor-pointer rounded-2xl border border-white/10 bg-white/[0.03] px-3 py-2.5 text-sm font-medium text-slate-200 transition hover:bg-white/[0.05]"
              type="button"
              onClick={() => {
                onManageConnectors();
                setIsOpen(false);
              }}
            >
              Manage connectors
            </button>
          </div>
        </div>
      ) : null}
    </div>
  );
}

function ChevronDownIcon() {
  return (
    <svg
      aria-hidden="true"
      className="h-4 w-4"
      fill="none"
      viewBox="0 0 24 24"
      stroke="currentColor"
      strokeWidth="1.8"
    >
      <path strokeLinecap="round" strokeLinejoin="round" d="m7 10 5 5 5-5" />
    </svg>
  );
}
