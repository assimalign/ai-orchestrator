import type { ModelSelection, RepositoryTarget } from "../../lib/models";

type ThreadContextBarProps = {
  apiBaseUrl: string;
  isPromoting: boolean;
  models?: ModelSelection;
  onPromote: () => Promise<void>;
  repository?: RepositoryTarget;
};

function ContextChip({ value }: { value: string }) {
  return (
    <span className="inline-flex items-center rounded-full border border-white/10 bg-white/5 px-4 py-2 text-xs font-medium tracking-[0.12em] text-slate-300">
      {value}
    </span>
  );
}

function workflowLabel(repository?: RepositoryTarget) {
  switch (repository?.workflowStatus) {
    case "readyForReview":
      return "Ready for review";
    case "promoted":
      return "Promoted upstream";
    case "failed":
      return "Branch workflow failed";
    default:
      return "Repository attached";
  }
}

export function ThreadContextBar({
  apiBaseUrl,
  isPromoting,
  models,
  onPromote,
  repository,
}: ThreadContextBarProps) {
  const canPromote =
    Boolean(repository?.workingBranch)
    && repository?.workflowStatus !== "promoted"
    && repository?.workflowStatus !== "failed";

  return (
    <div className="mt-6 rounded-[1.75rem] border border-white/10 bg-white/[0.045] p-5">
      <div className="flex flex-col gap-4 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <p className="text-[11px] font-semibold uppercase tracking-[0.32em] text-sage-300">
            Repository workspace
          </p>
          <h3 className="mt-2 text-xl font-semibold text-white">
            {repository ? `${repository.owner}/${repository.repo}` : "Attach a GitHub repository"}
          </h3>
          <p className="mt-2 text-sm leading-6 text-slate-400">
            {repository
              ? "Each thread now works against a repository workspace with a base branch, a working branch for iteration, and a target branch for promotion."
              : "Choose a GitHub repository and base branch before sending requirements so the thread can prepare a working branch."}
          </p>
        </div>

        <div className="flex flex-col items-start gap-3 xl:items-end">
          <ContextChip value={workflowLabel(repository)} />
          <div className="flex flex-wrap gap-3">
            {repository?.compareUrl ? (
              <a
                className="rounded-full border border-white/10 bg-white/5 px-4 py-2 text-sm font-medium text-slate-100 transition hover:bg-white/10"
                href={repository.compareUrl}
                target="_blank"
                rel="noreferrer"
              >
                Review branch
              </a>
            ) : null}
            <button
              className="rounded-full bg-sage-300 px-4 py-2 text-sm font-semibold text-ink-950 transition hover:bg-sage-200 disabled:cursor-not-allowed disabled:opacity-60"
              type="button"
              onClick={() => void onPromote()}
              disabled={!canPromote || isPromoting}
            >
              {isPromoting ? "Promoting..." : "Promote upstream"}
            </button>
          </div>
        </div>
      </div>

      <div className="mt-5 flex flex-wrap gap-3">
        <ContextChip
          value={
            repository
              ? `${repository.owner}/${repository.repo}`
              : "No GitHub repo attached"
          }
        />
        {repository?.connector ? (
          <ContextChip value={`Connector ${formatConnectorLabel(repository.connector)}`} />
        ) : null}
        {repository?.baseBranch ? (
          <ContextChip value={`Base ${repository.baseBranch}`} />
        ) : null}
        {repository?.workingBranch ? (
          <ContextChip value={`Working ${repository.workingBranch}`} />
        ) : null}
        {repository?.targetBranch ? (
          <ContextChip value={`Target ${repository.targetBranch}`} />
        ) : null}
        {models?.openAi ? <ContextChip value={`ChatGPT ${formatModelLabel(models.openAi)}`} /> : null}
        {models?.anthropic ? <ContextChip value={`Claude ${formatModelLabel(models.anthropic)}`} /> : null}
        <ContextChip value={`API ${apiBaseUrl}`} />
      </div>
    </div>
  );
}

function formatModelLabel(modelId: string) {
  return (
    {
      "gpt-5.4": "GPT-5.4",
      "gpt-5.4-mini": "GPT-5.4 mini",
      "gpt-5-codex": "GPT-5 Codex",
      "claude-sonnet-4-20250514": "Sonnet 4",
      "claude-opus-4-1-20250805": "Opus 4.1",
      "claude-3-7-sonnet-20250219": "3.7 Sonnet",
    }[modelId] ?? modelId
  );
}

function formatConnectorLabel(connectorId: string) {
  return connectorId === "github" ? "GitHub" : connectorId;
}
