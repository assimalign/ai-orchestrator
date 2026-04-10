import { ThreadSidebar } from "../../components/workspace/ThreadSidebar";
import { WorkspaceHeader } from "../../components/workspace/WorkspaceHeader";
import { ThreadContextBar } from "../../components/workspace/ThreadContextBar";
import { StageRail } from "../../components/workspace/StageRail";
import { ConversationFeed } from "../../components/workspace/ConversationFeed";
import { ComposerPanel } from "../../components/workspace/ComposerPanel";
import { ConnectorManagementModal } from "../../components/workspace/ConnectorManagementModal";
import { useAuth } from "../auth/auth-provider";
import { useWorkspace } from "./use-workspace";
import { runtimeConfig } from "../../lib/runtime-config";

export function WorkspacePage() {
  const auth = useAuth();
  const workspace = useWorkspace(auth.isAuthenticated);

  return (
    <div className="mx-auto grid min-h-screen max-w-[1680px] items-start gap-5 px-5 py-5 xl:grid-cols-[296px_minmax(0,1fr)]">
      <ThreadSidebar
        accountLabel={auth.accountLabel}
        activeThreadId={workspace.activeThread?.id}
        deletingThreadId={workspace.deletingThreadId}
        onDeleteThread={workspace.deleteThreadById}
        onSelectThread={workspace.setSelectedThreadId}
        onSignOut={auth.signOut}
        onStartNewThread={workspace.startNewThread}
        threads={workspace.threads}
      />

      <main className="flex min-h-[calc(100vh-2.5rem)] min-w-0 flex-col overflow-visible rounded-[2rem] border border-white/8 bg-[radial-gradient(circle_at_top,_rgba(148,199,176,0.06),transparent_24%),rgba(8,11,14,0.86)] p-6 shadow-[0_24px_90px_rgba(0,0,0,0.32)] backdrop-blur">
        <WorkspaceHeader
          activeThread={workspace.activeThread}
          config={workspace.config}
          statusMessage={workspace.statusMessage}
        />
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

        <section className="mt-6 flex min-h-0 flex-1 flex-col overflow-hidden">
          <ConversationFeed threadDetail={workspace.threadDetail} />
          <ComposerPanel
            anthropicModel={workspace.anthropicModel}
            anthropicReasoningEffort={workspace.anthropicReasoningEffort}
            baseBranch={workspace.baseBranch}
            branchOptions={workspace.connectorBranches}
            connectorId={workspace.connectorId}
            connectorRepositories={workspace.connectorRepositories}
            config={workspace.config}
            createWorkingBranchFromDefault={workspace.createWorkingBranchFromDefault}
            draft={workspace.draft}
            hasActiveThread={Boolean(workspace.selectedThreadId)}
            isLoadingBranches={workspace.isLoadingBranches}
            isLoadingRepositories={workspace.isLoadingRepositories}
            isListening={workspace.isListening}
            isSending={workspace.isSending}
            isSpeaking={workspace.isSpeaking}
            latestAssistantMessage={workspace.latestAssistantMessage}
            onAnthropicModelChange={workspace.setAnthropicModel}
            onAnthropicReasoningEffortChange={workspace.setAnthropicReasoningEffort}
            onBaseBranchChange={workspace.setBaseBranch}
            onCaptureSpeech={workspace.captureSpeech}
            onConnectorChange={workspace.setConnectorId}
            onDraftChange={workspace.setDraft}
            onManageConnectors={workspace.manageConnectors}
            onOpenAiModelChange={workspace.setOpenAiModel}
            onOpenAiReasoningEffortChange={workspace.setOpenAiReasoningEffort}
            onRepositorySelect={workspace.selectRepository}
            onSpeakLatestResponse={workspace.speakLatestResponse}
            onSubmit={workspace.submitMessage}
            onTargetBranchChange={workspace.setTargetBranch}
            openAiModel={workspace.openAiModel}
            openAiReasoningEffort={workspace.openAiReasoningEffort}
            owner={workspace.owner}
            repo={workspace.repo}
            statusMessage={workspace.statusMessage}
            targetBranch={workspace.targetBranch}
          />
        </section>
      </main>

      {workspace.isConnectorManagerOpen ? (
        <ConnectorManagementModal
          connectors={workspace.config?.connectors ?? []}
          isLoading={workspace.isLoadingConnectorStatuses}
          onClose={workspace.closeConnectorManager}
          onRefresh={workspace.refreshConnectorStatuses}
          statuses={workspace.connectorStatuses}
        />
      ) : null}
    </div>
  );
}
