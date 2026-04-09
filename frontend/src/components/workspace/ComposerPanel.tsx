import type { FormEvent, ReactNode } from "react";
import type { AppConfigResponse, ModelOption, ThreadMessage } from "../../lib/models";

type ComposerPanelProps = {
  anthropicModel: string;
  baseBranch: string;
  config?: AppConfigResponse;
  draft: string;
  hasActiveThread: boolean;
  isListening: boolean;
  isSending: boolean;
  isSpeaking: boolean;
  latestAssistantMessage?: ThreadMessage;
  onAnthropicModelChange: (value: string) => void;
  onBaseBranchChange: (value: string) => void;
  onCaptureSpeech: () => Promise<void>;
  onDraftChange: (value: string) => void;
  onOpenAiModelChange: (value: string) => void;
  onOwnerChange: (value: string) => void;
  onRepoChange: (value: string) => void;
  onSpeakLatestResponse: () => Promise<void>;
  onSubmit: () => Promise<void>;
  openAiModel: string;
  owner: string;
  repo: string;
  statusMessage: string;
  targetBranch: string;
  onTargetBranchChange: (value: string) => void;
};

export function ComposerPanel({
  anthropicModel,
  baseBranch,
  config,
  draft,
  hasActiveThread,
  isListening,
  isSending,
  isSpeaking,
  latestAssistantMessage,
  onAnthropicModelChange,
  onBaseBranchChange,
  onCaptureSpeech,
  onDraftChange,
  onOpenAiModelChange,
  onOwnerChange,
  onRepoChange,
  onSpeakLatestResponse,
  onSubmit,
  openAiModel,
  owner,
  repo,
  statusMessage,
  targetBranch,
  onTargetBranchChange,
}: ComposerPanelProps) {
  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await onSubmit();
  }

  return (
    <section className="mt-5 space-y-3">
      <div className="grid gap-2 xl:grid-cols-[minmax(0,1.5fr)_repeat(2,minmax(0,0.8fr))]">
        <CompactField
          label="Repository"
          placeholder="owner/repo"
          value={owner || repo ? `${owner}${repo ? `/${repo}` : ""}` : ""}
          onChange={(value) => {
            const [nextOwner, ...repoParts] = value.split("/");
            onOwnerChange(nextOwner ?? "");
            onRepoChange(repoParts.join("/"));
          }}
        />
        <CompactField
          label="Base"
          placeholder="main"
          value={baseBranch}
          onChange={onBaseBranchChange}
        />
        <CompactField
          label="Target"
          placeholder="feature/ai-dev"
          value={targetBranch}
          onChange={onTargetBranchChange}
        />
      </div>

      <form
        className="overflow-hidden rounded-[1.9rem] border border-white/10 bg-[#2a2a2b]/95 shadow-panel"
        onSubmit={handleSubmit}
      >
        <textarea
          className="min-h-[188px] w-full resize-none border-0 bg-transparent px-5 py-5 text-[15px] leading-7 text-slate-100 outline-none placeholder:text-slate-500"
          value={draft}
          onChange={(event) => onDraftChange(event.target.value)}
          placeholder="Ask for follow-up changes"
          rows={6}
        />

        <div className="border-t border-white/5 px-3 py-3">
          <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
            <div className="flex flex-wrap items-center gap-2.5">
              <IconButton
                label={isListening ? "Listening..." : "Use microphone"}
                onClick={() => void onCaptureSpeech()}
                disabled={!config?.speechEnabled || isListening}
              >
                <MicrophoneIcon />
              </IconButton>

              <ToolbarSelect
                label="ChatGPT"
                value={openAiModel}
                options={config?.models.openAi ?? []}
                onChange={onOpenAiModelChange}
                disabled={!config?.providers.openAi}
              />

              <ToolbarSelect
                label="Claude"
                value={anthropicModel}
                options={config?.models.anthropic ?? []}
                onChange={onAnthropicModelChange}
                disabled={!config?.providers.anthropic}
              />

              <IconButton
                label={isSpeaking ? "Speaking..." : "Read latest response"}
                onClick={() => void onSpeakLatestResponse()}
                disabled={!config?.speechEnabled || !latestAssistantMessage || isSpeaking}
              >
                <SpeakerIcon />
              </IconButton>
            </div>

            <button
              className="inline-flex items-center justify-center rounded-full bg-sage-300 px-5 py-2.5 text-sm font-semibold text-ink-950 transition hover:bg-sage-200 disabled:cursor-not-allowed disabled:opacity-60"
              type="submit"
              disabled={!draft.trim() || isSending}
            >
              {isSending
                ? "Sending..."
                : hasActiveThread
                  ? "Reply in thread"
                  : "Start thread"}
            </button>
          </div>

          <p className="mt-3 px-1 text-xs leading-5 text-slate-400">
            {statusMessage}
          </p>
        </div>
      </form>
    </section>
  );
}

function CompactField({
  label,
  onChange,
  placeholder,
  value,
}: {
  label: string;
  onChange: (value: string) => void;
  placeholder: string;
  value: string;
}) {
  return (
    <label className="rounded-[1.2rem] border border-white/10 bg-white/[0.04] px-3.5 py-3">
      <span className="text-[10px] font-semibold uppercase tracking-[0.26em] text-slate-500">
        {label}
      </span>
      <input
        className="mt-2 w-full border-0 bg-transparent p-0 text-sm text-slate-100 outline-none placeholder:text-slate-500"
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder={placeholder}
      />
    </label>
  );
}

function ToolbarSelect({
  disabled,
  label,
  onChange,
  options,
  value,
}: {
  disabled?: boolean;
  label: string;
  onChange: (value: string) => void;
  options: ModelOption[];
  value: string;
}) {
  const resolvedValue = value || options[0]?.id || "";

  return (
    <label className="relative inline-flex items-center">
      <span className="sr-only">{label}</span>
      <select
        className="appearance-none rounded-full border border-white/10 bg-[#343436] px-3.5 py-2 pr-9 text-sm font-medium text-slate-200 outline-none transition hover:bg-[#3c3c3f] disabled:cursor-not-allowed disabled:opacity-50"
        disabled={disabled || options.length === 0}
        value={resolvedValue}
        onChange={(event) => onChange(event.target.value)}
      >
        {options.map((option) => (
          <option key={option.id} value={option.id} className="bg-[#343436] text-slate-100">
            {option.label}
          </option>
        ))}
      </select>
      <span className="pointer-events-none absolute right-3 text-slate-500">
        <ChevronDownIcon />
      </span>
    </label>
  );
}

function IconButton({
  children,
  disabled,
  label,
  onClick,
}: {
  children: ReactNode;
  disabled?: boolean;
  label: string;
  onClick: () => void;
}) {
  return (
    <button
      className="inline-flex h-10 w-10 items-center justify-center rounded-full border border-white/10 bg-white/[0.04] text-slate-200 transition hover:bg-white/[0.08] disabled:cursor-not-allowed disabled:opacity-50"
      type="button"
      aria-label={label}
      title={label}
      onClick={onClick}
      disabled={disabled}
    >
      {children}
    </button>
  );
}

function MicrophoneIcon() {
  return (
    <svg
      aria-hidden="true"
      className="h-4 w-4"
      fill="none"
      viewBox="0 0 24 24"
      stroke="currentColor"
      strokeWidth="1.8"
    >
      <path strokeLinecap="round" strokeLinejoin="round" d="M12 4a3 3 0 0 0-3 3v5a3 3 0 0 0 6 0V7a3 3 0 0 0-3-3Z" />
      <path strokeLinecap="round" strokeLinejoin="round" d="M6 11.5a6 6 0 0 0 12 0M12 17.5v3.5M8.5 21h7" />
    </svg>
  );
}

function SpeakerIcon() {
  return (
    <svg
      aria-hidden="true"
      className="h-4 w-4"
      fill="none"
      viewBox="0 0 24 24"
      stroke="currentColor"
      strokeWidth="1.8"
    >
      <path strokeLinecap="round" strokeLinejoin="round" d="M11 5 6.8 8.5H4a1 1 0 0 0-1 1v5a1 1 0 0 0 1 1h2.8L11 19V5Z" />
      <path strokeLinecap="round" strokeLinejoin="round" d="M15.5 9.5a4 4 0 0 1 0 5M18.5 7a7.5 7.5 0 0 1 0 10" />
    </svg>
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
