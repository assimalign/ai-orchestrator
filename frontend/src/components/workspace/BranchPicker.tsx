import { useEffect, useMemo, useRef, useState } from "react";
import type { ConnectorBranchReference } from "../../lib/models";

type BranchPickerProps = {
  branches: ConnectorBranchReference[];
  disabled?: boolean;
  isLoading: boolean;
  label: string;
  onCreateFromDefault: () => void;
  onSelectBranch: (branchName: string) => void;
  placeholder: string;
  selectedBranch: string;
};

export function BranchPicker({
  branches,
  disabled,
  isLoading,
  label,
  onCreateFromDefault,
  onSelectBranch,
  placeholder,
  selectedBranch,
}: BranchPickerProps) {
  const [isOpen, setIsOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement | null>(null);
  const selected = useMemo(
    () => branches.find((branch) => branch.name === selectedBranch),
    [branches, selectedBranch],
  );
  const defaultBranch = useMemo(
    () => branches.find((branch) => branch.isDefault),
    [branches],
  );

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
        className="flex w-full cursor-pointer items-center justify-between gap-3 rounded-[1.2rem] border border-white/8 bg-white/[0.03] px-3.5 py-3 text-left transition hover:bg-white/[0.06] disabled:cursor-not-allowed disabled:opacity-60"
        type="button"
        onClick={() => setIsOpen((current) => !current)}
        disabled={disabled}
      >
        <div className="min-w-0">
          <span className="text-[10px] font-semibold uppercase tracking-[0.26em] text-slate-500">
            {label}
          </span>
          <div className="mt-2 flex min-w-0 items-center gap-2">
            <span className="truncate text-sm text-slate-100">
              {selected?.name || selectedBranch || placeholder}
            </span>
            {selected?.isDefault ? (
              <span className="rounded-full border border-white/8 bg-white/[0.04] px-2 py-1 text-[10px] font-semibold uppercase tracking-[0.18em] text-sage-300">
                Default
              </span>
            ) : null}
          </div>
        </div>
        <span className="shrink-0 text-slate-500">
          <ChevronDownIcon />
        </span>
      </button>

      {isOpen ? (
        <div className="absolute left-0 z-40 mt-2 w-full min-w-[22rem] overflow-hidden rounded-[1.4rem] border border-white/8 bg-[#16181c]/98 shadow-[0_20px_60px_rgba(0,0,0,0.42)] backdrop-blur">
          <div className="border-b border-white/8 p-3">
            <button
              className="flex w-full cursor-pointer items-center justify-between rounded-2xl border border-sage-300/20 bg-sage-300/10 px-3.5 py-3 text-left transition hover:bg-sage-300/15"
              type="button"
              onClick={() => {
                onCreateFromDefault();
                setIsOpen(false);
              }}
            >
              <div>
                <p className="text-sm font-semibold text-sage-100">Create new working branch</p>
                <p className="mt-1 text-xs text-sage-200/80">
                  {defaultBranch
                    ? `Start from ${defaultBranch.name} and let the thread create a fresh working branch.`
                    : "Start from the repository default branch and let the thread create a fresh working branch."}
                </p>
              </div>
              <span className="rounded-full border border-sage-300/20 bg-black/20 px-2 py-1 text-[10px] font-semibold uppercase tracking-[0.18em] text-sage-100">
                Quick
              </span>
            </button>
          </div>

          <div className="max-h-[22rem] overflow-y-auto p-3">
            {isLoading ? (
              <div className="rounded-2xl border border-white/10 bg-white/[0.03] px-3 py-4 text-sm text-slate-400">
                Loading branches...
              </div>
            ) : branches.length > 0 ? (
              <div className="space-y-1.5">
                {branches.map((branch) => {
                  const branchSelected = branch.name === selectedBranch;

                  return (
                    <button
                      key={branch.name}
                      className={`flex w-full cursor-pointer items-center justify-between rounded-2xl border px-3 py-3 text-left transition ${
                        branchSelected
                          ? "border-sage-300/25 bg-sage-300/10"
                          : "border-white/10 bg-white/[0.03] hover:bg-white/[0.05]"
                      }`}
                      type="button"
                      onClick={() => {
                        onSelectBranch(branch.name);
                        setIsOpen(false);
                      }}
                    >
                      <div className="min-w-0">
                        <p className="truncate text-sm font-medium text-white">{branch.name}</p>
                        <p className="mt-1 text-xs text-slate-500">
                          {branch.isDefault
                            ? "Default branch"
                            : branch.isProtected
                              ? "Protected branch"
                              : "Available branch"}
                        </p>
                      </div>
                      <div className="flex flex-wrap items-center gap-2">
                        {branch.isProtected ? (
                          <span className="rounded-full bg-sky-400/10 px-2 py-1 text-[10px] font-semibold uppercase tracking-[0.18em] text-sky-200">
                            Protected
                          </span>
                        ) : null}
                        {branch.isDefault ? (
                          <span className="rounded-full bg-sage-300/15 px-2 py-1 text-[10px] font-semibold uppercase tracking-[0.18em] text-sage-200">
                            Default
                          </span>
                        ) : null}
                      </div>
                    </button>
                  );
                })}
              </div>
            ) : (
              <div className="rounded-2xl border border-white/10 bg-white/[0.03] px-3 py-4 text-sm text-slate-400">
                Select a repository to load branches.
              </div>
            )}
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
