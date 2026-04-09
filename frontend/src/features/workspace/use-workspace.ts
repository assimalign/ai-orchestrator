import { useEffect, useMemo, useState } from "react";
import {
  buildRepositoryTarget,
  createThread,
  getConnectorStatus,
  getConfig,
  getSpeechToken,
  getThread,
  listConnectorRepositories,
  listThreads,
  postThreadMessage,
  promoteThread,
} from "../../lib/api";
import type {
  AppConfigResponse,
  ConnectorDefinition,
  ConnectorRepositoryReference,
  ConnectorStatusResponse,
  ConversationThread,
  ConversationThreadDetail,
  ModelSelection,
  ThreadMessage,
  ThreadStageStatus,
} from "../../lib/models";
import { runtimeConfig } from "../../lib/runtime-config";

const activeStatuses: ThreadStageStatus[] = [
  "queued",
  "planning",
  "reviewing",
  "synthesizing",
];

const compactStageLimit = 4;

async function loadSpeechSdk() {
  return import("microsoft-cognitiveservices-speech-sdk");
}

export function useWorkspace(enabled: boolean) {
  const [config, setConfig] = useState<AppConfigResponse>();
  const [threads, setThreads] = useState<ConversationThread[]>([]);
  const [threadDetail, setThreadDetail] = useState<ConversationThreadDetail>();
  const [selectedThreadId, setSelectedThreadId] = useState<string>();
  const [draft, setDraft] = useState("");
  const [connectorId, setConnectorId] = useState("github");
  const [connectorRepositories, setConnectorRepositories] = useState<ConnectorRepositoryReference[]>([]);
  const [owner, setOwner] = useState("");
  const [repo, setRepo] = useState("");
  const [baseBranch, setBaseBranch] = useState("");
  const [targetBranch, setTargetBranch] = useState("");
  const [openAiModel, setOpenAiModel] = useState("");
  const [anthropicModel, setAnthropicModel] = useState("");
  const [statusMessage, setStatusMessage] = useState("Preparing your workspace.");
  const [isLoadingRepositories, setIsLoadingRepositories] = useState(false);
  const [isConnectorManagerOpen, setIsConnectorManagerOpen] = useState(false);
  const [isLoadingConnectorStatuses, setIsLoadingConnectorStatuses] = useState(false);
  const [connectorStatuses, setConnectorStatuses] = useState<Record<string, ConnectorStatusResponse>>({});
  const [isSending, setIsSending] = useState(false);
  const [isPromoting, setIsPromoting] = useState(false);
  const [isListening, setIsListening] = useState(false);
  const [isSpeaking, setIsSpeaking] = useState(false);

  const activeThread = useMemo(
    () =>
      threadDetail?.thread ?? threads.find((thread) => thread.id === selectedThreadId),
    [selectedThreadId, threadDetail, threads],
  );

  const latestAssistantMessage = useMemo(() => {
    return [...(threadDetail?.messages ?? [])]
      .reverse()
      .find((message) => message.role === "assistant");
  }, [threadDetail]);

  const stageMessages = useMemo(() => {
    return (threadDetail?.messages ?? [])
      .filter((message) => message.role === "stage")
      .slice(-compactStageLimit);
  }, [threadDetail]);

  useEffect(() => {
    if (!enabled) {
      return;
    }

    void loadShell();
  }, [enabled]);

  useEffect(() => {
    if (!enabled || !selectedThreadId) {
      return;
    }

    void loadThread(selectedThreadId);
  }, [enabled, selectedThreadId]);

  useEffect(() => {
    if (!enabled || !activeThread || !activeStatuses.includes(activeThread.status)) {
      return;
    }

    const timer = window.setInterval(() => {
      void Promise.all([refreshThreads(), loadThread(activeThread.id)]);
    }, 4000);

    return () => window.clearInterval(timer);
  }, [activeThread, enabled]);

  useEffect(() => {
    if (!config || selectedThreadId) {
      return;
    }

    const defaultConnector = getDefaultConnector(config.connectors);
    setConnectorId((current) => current || defaultConnector?.id || "github");
    setOpenAiModel((current) => current || config.models.defaults.openAi || config.models.openAi[0]?.id || "");
    setAnthropicModel(
      (current) =>
        current
        || config.models.defaults.anthropic
        || config.models.anthropic[0]?.id
        || "",
    );
  }, [config, selectedThreadId]);

  useEffect(() => {
    if (!threadDetail?.thread.repository) {
      if (threadDetail?.thread) {
        setConnectorId(getDefaultConnector(config?.connectors)?.id ?? "github");
        setOwner("");
        setRepo("");
        setBaseBranch("");
        setTargetBranch("");
        applyModels(threadDetail.thread.models, config, setOpenAiModel, setAnthropicModel);
      }

      return;
    }

    setConnectorId(threadDetail.thread.repository.connector ?? "github");
    setOwner(threadDetail.thread.repository.owner ?? "");
    setRepo(threadDetail.thread.repository.repo ?? "");
    setBaseBranch(
      threadDetail.thread.repository.baseBranch
        ?? threadDetail.thread.repository.targetBranch
        ?? threadDetail.thread.repository.defaultBranch
        ?? "",
    );
    setTargetBranch(
      threadDetail.thread.repository.targetBranch
        ?? threadDetail.thread.repository.baseBranch
        ?? threadDetail.thread.repository.defaultBranch
        ?? "",
    );
    applyModels(threadDetail.thread.models, config, setOpenAiModel, setAnthropicModel);
  }, [config, threadDetail?.thread.id]);

  useEffect(() => {
    if (!enabled || !config) {
      return;
    }

    const connector = resolveConnector(config.connectors, connectorId);
    if (!connector?.enabled) {
      setConnectorRepositories([]);
      return;
    }

    void loadConnectorRepositories(connector.id, { preserveStatusMessage: true });
  }, [config, connectorId, enabled]);

  async function loadShell() {
    try {
      const [nextConfig] = await Promise.all([getConfig(), refreshThreads()]);
      setConfig(nextConfig);
      const defaultConnector = getDefaultConnector(nextConfig.connectors);
      if (defaultConnector) {
        setConnectorId(defaultConnector.id);
      }

      setStatusMessage("Choose a thread or start a new one.");
    } catch (error) {
      setStatusMessage(formatWorkspaceError(error, "Could not load the orchestrator shell."));
    }
  }

  async function refreshThreads() {
    const nextThreads = await listThreads();
    setThreads(nextThreads);

    if (!selectedThreadId && nextThreads[0]) {
      setSelectedThreadId(nextThreads[0].id);
    }

    if (
      selectedThreadId &&
      !nextThreads.some((thread) => thread.id === selectedThreadId)
    ) {
      setSelectedThreadId(nextThreads[0]?.id);
    }

    return nextThreads;
  }

  async function loadThread(threadId: string) {
    try {
      const detail = await getThread(threadId);
      setThreadDetail(detail);
    } catch (error) {
      setStatusMessage(formatWorkspaceError(error, "Unable to load that thread."));
    }
  }

  function startNewThread() {
    setSelectedThreadId(undefined);
    setThreadDetail(undefined);
    setDraft("");
    setConnectorId(getDefaultConnector(config?.connectors)?.id ?? "github");
    setOwner("");
    setRepo("");
    setBaseBranch("");
    setTargetBranch("");
    setOpenAiModel(config?.models.defaults.openAi ?? config?.models.openAi[0]?.id ?? "");
    setAnthropicModel(config?.models.defaults.anthropic ?? config?.models.anthropic[0]?.id ?? "");
    setStatusMessage("Starting a new thread.");
  }

  async function submitMessage() {
    if (!draft.trim()) {
      return;
    }

    setIsSending(true);
    setStatusMessage(
      selectedThreadId
        ? "Sending your update into the active thread."
        : "Creating a new thread and dispatching it to Codex and Claude.",
    );

    try {
      const input = {
        text: draft.trim(),
        repository: buildRepositoryTarget(connectorId, owner, repo, baseBranch, targetBranch),
        models: buildModelSelection(openAiModel, anthropicModel),
      };

      const detail = selectedThreadId
        ? await postThreadMessage(selectedThreadId, input)
        : await createThread(input);

      setDraft("");
      setThreadDetail(detail);
      setSelectedThreadId(detail.thread.id);
      await refreshThreads();
      setStatusMessage(
        detail.thread.repository?.workflowStatus === "readyForReview"
          ? "Working branch ready for review."
          : detail.thread.status === "completed"
            ? "Response ready."
          : "Thread updated. The agents are working through the stages.",
      );
    } catch (error) {
      setStatusMessage(formatWorkspaceError(error, "Unable to send the message."));
    } finally {
      setIsSending(false);
    }
  }

  async function promoteActiveThread() {
    if (!selectedThreadId) {
      return;
    }

    setIsPromoting(true);
    setStatusMessage("Promoting the working branch into the target branch.");

    try {
      const detail = await promoteThread(selectedThreadId);
      setThreadDetail(detail);
      await refreshThreads();
      setStatusMessage("The working branch was promoted to the target branch.");
    } catch (error) {
      setStatusMessage(formatWorkspaceError(error, "Unable to promote the branch."));
    } finally {
      setIsPromoting(false);
    }
  }

  async function captureSpeech() {
    setIsListening(true);
    setStatusMessage("Listening for your instruction.");

    try {
      const SpeechSdk = await loadSpeechSdk();
      const speechToken = await getSpeechToken();
      const speechConfig = SpeechSdk.SpeechConfig.fromAuthorizationToken(
        speechToken.token,
        speechToken.region,
      );
      speechConfig.speechRecognitionLanguage = "en-US";

      const recognizer = new SpeechSdk.SpeechRecognizer(
        speechConfig,
        SpeechSdk.AudioConfig.fromDefaultMicrophoneInput(),
      );

      const recognizedText = await new Promise<string>((resolve, reject) => {
        recognizer.recognizeOnceAsync(
          (result) => {
            recognizer.close();

            if (result.reason === SpeechSdk.ResultReason.RecognizedSpeech) {
              resolve(result.text);
            } else {
              reject(new Error("No speech was recognized."));
            }
          },
          (error) => {
            recognizer.close();
            reject(new Error(String(error)));
          },
        );
      });

      setDraft((current) => [current, recognizedText].filter(Boolean).join(" ").trim());
      setStatusMessage("Transcription captured.");
    } catch (error) {
      setStatusMessage(formatWorkspaceError(error, "Voice capture failed."));
    } finally {
      setIsListening(false);
    }
  }

  async function speakLatestResponse() {
    if (!latestAssistantMessage) {
      return;
    }

    setIsSpeaking(true);
    setStatusMessage("Reading the latest Codex response.");

    try {
      const SpeechSdk = await loadSpeechSdk();
      const speechToken = await getSpeechToken();
      const speechConfig = SpeechSdk.SpeechConfig.fromAuthorizationToken(
        speechToken.token,
        speechToken.region,
      );
      speechConfig.speechSynthesisVoiceName = speechToken.voice;

      const synthesizer = new SpeechSdk.SpeechSynthesizer(
        speechConfig,
        SpeechSdk.AudioConfig.fromDefaultSpeakerOutput(),
      );

      await new Promise<void>((resolve, reject) => {
        synthesizer.speakTextAsync(
          latestAssistantMessage.content,
          (result) => {
            synthesizer.close();

            if (
              result.reason ===
              SpeechSdk.ResultReason.SynthesizingAudioCompleted
            ) {
              resolve();
            } else {
              reject(new Error("Speech synthesis did not complete."));
            }
          },
          (error) => {
            synthesizer.close();
            reject(new Error(String(error)));
          },
        );
      });

      setStatusMessage("Playback finished.");
    } catch (error) {
      setStatusMessage(formatWorkspaceError(error, "Unable to read the response."));
    } finally {
      setIsSpeaking(false);
    }
  }

  function selectRepository(reference: ConnectorRepositoryReference) {
    setConnectorId(reference.connectorId);
    setOwner(reference.owner);
    setRepo(reference.repo);
    setBaseBranch(reference.defaultBranch || "main");
    setTargetBranch((current) => current || reference.defaultBranch || "main");
    setStatusMessage(`Attached ${reference.owner}/${reference.repo} from ${formatConnectorLabel(reference.connectorId)}.`);
  }

  async function loadConnectorRepositories(
    nextConnectorId: string,
    options?: { preserveStatusMessage?: boolean },
  ) {
    setIsLoadingRepositories(true);

    try {
      const repositories = await listConnectorRepositories(nextConnectorId);
      setConnectorRepositories(repositories);

      if (!options?.preserveStatusMessage) {
        setStatusMessage(
          repositories.length > 0
            ? `Loaded ${repositories.length} repositories from ${formatConnectorLabel(nextConnectorId)}.`
            : `${formatConnectorLabel(nextConnectorId)} is connected, but no repositories were returned.`,
        );
      }
    } catch (error) {
      setConnectorRepositories([]);
      if (!options?.preserveStatusMessage) {
        setStatusMessage(
          formatWorkspaceError(
            error,
            `Unable to load repositories from ${formatConnectorLabel(nextConnectorId)}.`,
          ),
        );
      }
    } finally {
      setIsLoadingRepositories(false);
    }
  }

  function handleConnectorChange(nextConnectorId: string) {
    setConnectorId(nextConnectorId);
    setOwner("");
    setRepo("");
    setBaseBranch("");
    setTargetBranch("");
    setStatusMessage(`Switching repository connector to ${formatConnectorLabel(nextConnectorId)}.`);
  }

  async function refreshConnectorStatuses(options?: { preserveStatusMessage?: boolean }) {
    if (!config?.connectors?.length) {
      return;
    }

    setIsLoadingConnectorStatuses(true);

    try {
      const statuses = await Promise.all(
        config.connectors.map(async (connector) => [connector.id, await getConnectorStatus(connector.id)] as const),
      );

      setConnectorStatuses(Object.fromEntries(statuses));

      if (!options?.preserveStatusMessage) {
        setStatusMessage("Connector runtime status refreshed.");
      }
    } catch (error) {
      if (!options?.preserveStatusMessage) {
        setStatusMessage(formatWorkspaceError(error, "Unable to refresh connector status."));
      }
    } finally {
      setIsLoadingConnectorStatuses(false);
    }
  }

  function manageConnectors() {
    setIsConnectorManagerOpen(true);
    setStatusMessage("Reviewing connector access and runtime readiness.");
    void refreshConnectorStatuses({ preserveStatusMessage: true });
  }

  function closeConnectorManager() {
    setIsConnectorManagerOpen(false);
    setStatusMessage("Back to the workspace.");
  }

  return {
    activeThread,
    anthropicModel,
    connectorId,
    connectorRepositories,
    config,
    connectorStatuses,
    closeConnectorManager,
    draft,
    baseBranch,
    isConnectorManagerOpen,
    isLoadingRepositories,
    isLoadingConnectorStatuses,
    isListening,
    isPromoting,
    isSending,
    isSpeaking,
    latestAssistantMessage,
    owner,
    openAiModel,
    manageConnectors,
    repo,
    refreshConnectorStatuses,
    selectRepository,
    selectedThreadId,
    setAnthropicModel,
    setBaseBranch,
    setConnectorId: handleConnectorChange,
    setDraft,
    setOpenAiModel,
    setOwner,
    setRepo,
    setTargetBranch,
    setSelectedThreadId,
    stageMessages,
    startNewThread,
    statusMessage,
    submitMessage,
    promoteActiveThread,
    captureSpeech,
    speakLatestResponse,
    targetBranch,
    threadDetail,
    threads,
  };
}

export type WorkspaceState = ReturnType<typeof useWorkspace>;
export type StageMessage = ThreadMessage;

function buildModelSelection(
  openAi: string,
  anthropic: string,
): ModelSelection | undefined {
  const nextSelection: ModelSelection = {
    openAi: openAi.trim() || undefined,
    anthropic: anthropic.trim() || undefined,
  };

  return nextSelection.openAi || nextSelection.anthropic ? nextSelection : undefined;
}

function applyModels(
  selection: ModelSelection | undefined,
  config: AppConfigResponse | undefined,
  setOpenAiModel: (value: string) => void,
  setAnthropicModel: (value: string) => void,
) {
  setOpenAiModel(
    selection?.openAi
      ?? config?.models.defaults.openAi
      ?? config?.models.openAi[0]?.id
      ?? "",
  );
  setAnthropicModel(
    selection?.anthropic
      ?? config?.models.defaults.anthropic
      ?? config?.models.anthropic[0]?.id
      ?? "",
  );
}

function formatWorkspaceError(error: unknown, fallback: string) {
  const message = error instanceof Error ? error.message : fallback;

  if (message.includes("AADSTS500011") || message.includes("invalid_resource")) {
    return [
      "Microsoft Entra API scope is not configured for this app registration.",
      `The app is requesting api://${runtimeConfig.entraClientId}/access_as_user.`,
      "In the app registration, open `Expose an API`, set the Application ID URI to match that value, and add a delegated scope named `access_as_user`.",
    ].join(" ");
  }

  return message;
}

function getDefaultConnector(connectors?: ConnectorDefinition[]) {
  return connectors?.find((connector) => connector.enabled) ?? connectors?.[0];
}

function resolveConnector(
  connectors: ConnectorDefinition[] | undefined,
  connectorId: string,
) {
  return connectors?.find((connector) => connector.id === connectorId);
}

function formatConnectorLabel(connectorId: string) {
  return connectorId === "github" ? "GitHub" : connectorId;
}
