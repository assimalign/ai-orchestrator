import { ThreadSidebar } from "../../components/workspace/ThreadSidebar";
import { WorkspaceHeader } from "../../components/workspace/WorkspaceHeader";
import { ThreadContextBar } from "../../components/workspace/ThreadContextBar";
import { StageRail } from "../../components/workspace/StageRail";
import { ConversationFeed } from "../../components/workspace/ConversationFeed";
import { ComposerPanel } from "../../components/workspace/ComposerPanel";
import { useAuth } from "../auth/auth-provider";
import { useWorkspace } from "./use-workspace";
import { runtimeConfig } from "../../lib/runtime-config";

export function WorkspacePage() {
  const auth = useAuth();
  const workspace = useWorkspace(auth.isAuthenticated);

  return (
    <div className="mx-auto grid min-h-screen max-w-[1600px] gap-4 px-4 py-4 xl:grid-cols-[320px_minmax(0,1fr)]">
      <ThreadSidebar
        accountLabel={auth.accountLabel}
        activeThreadId={workspace.activeThread?.id}
        onSelectThread={workspace.setSelectedThreadId}
        onSignOut={auth.signOut}
        onStartNewThread={workspace.startNewThread}
        threads={workspace.threads}
      />

      <main className="flex min-h-[calc(100vh-2rem)] flex-col rounded-[2rem] border border-white/10 bg-black/20 p-5 backdrop-blur">
        <WorkspaceHeader activeThread={workspace.activeThread} config={workspace.config} />
        <ThreadContextBar
          apiBaseUrl={runtimeConfig.apiBaseUrl}
          isPromoting={workspace.isPromoting}
          models={workspace.activeThread?.models}
          onPromote={workspace.promoteActiveThread}
          repository={workspace.activeThread?.repository}
        />
        <StageRail
          activeStatus={workspace.activeThread?.status}
          messages={workspace.stageMessages}
        />

        <section className="mt-6 grid min-h-0 flex-1 grid-rows-[minmax(0,1fr)_auto]">
          <ConversationFeed threadDetail={workspace.threadDetail} />
          <ComposerPanel
            anthropicModel={workspace.anthropicModel}
            baseBranch={workspace.baseBranch}
            config={workspace.config}
            draft={workspace.draft}
            hasActiveThread={Boolean(workspace.selectedThreadId)}
            isListening={workspace.isListening}
            isSending={workspace.isSending}
            isSpeaking={workspace.isSpeaking}
            latestAssistantMessage={workspace.latestAssistantMessage}
            onAnthropicModelChange={workspace.setAnthropicModel}
            onBaseBranchChange={workspace.setBaseBranch}
            onCaptureSpeech={workspace.captureSpeech}
            onDraftChange={workspace.setDraft}
            onOpenAiModelChange={workspace.setOpenAiModel}
            onOwnerChange={workspace.setOwner}
            onRepoChange={workspace.setRepo}
            onSpeakLatestResponse={workspace.speakLatestResponse}
            onSubmit={workspace.submitMessage}
            onTargetBranchChange={workspace.setTargetBranch}
            openAiModel={workspace.openAiModel}
            owner={workspace.owner}
            repo={workspace.repo}
            statusMessage={workspace.statusMessage}
            targetBranch={workspace.targetBranch}
          />
        </section>
      </main>
    </div>
  );
}
