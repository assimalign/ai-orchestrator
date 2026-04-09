export type ThreadStageStatus =
  | "queued"
  | "planning"
  | "reviewing"
  | "synthesizing"
  | "completed"
  | "failed";

export type ThreadMessageRole = "user" | "assistant" | "stage" | "system";

export type RepositoryWorkflowStatus =
  | "attached"
  | "readyForReview"
  | "promoted"
  | "failed";

export interface RepositoryTarget {
  connector?: string;
  owner: string;
  repo: string;
  branch?: string;
  baseBranch?: string;
  workingBranch?: string;
  targetBranch?: string;
  defaultBranch?: string;
  url?: string;
  branchUrl?: string;
  compareUrl?: string;
  lastPromotionCommitSha?: string;
  preparedAt?: string;
  promotedAt?: string;
  workflowStatus?: RepositoryWorkflowStatus;
  issueNumber?: number;
  pullRequestNumber?: number;
}

export interface ConversationInput {
  text: string;
  repository?: RepositoryTarget;
  models?: ModelSelection;
}

export interface ConversationThread {
  id: string;
  title: string;
  status: ThreadStageStatus;
  createdAt: string;
  updatedAt: string;
  repository?: RepositoryTarget;
  models?: ModelSelection;
  lastMessagePreview: string;
  summary?: string;
  error?: string;
}

export interface ThreadMessage {
  id: string;
  threadId: string;
  role: ThreadMessageRole;
  stage?: ThreadStageStatus;
  title: string;
  content: string;
  provider?: string;
  createdAt: string;
  metadata?: Record<string, string>;
}

export interface ConversationThreadDetail {
  thread: ConversationThread;
  messages: ThreadMessage[];
}

export interface ProviderAvailability {
  openAi: boolean;
  anthropic: boolean;
}

export interface ModelSelection {
  openAi?: string;
  anthropic?: string;
}

export interface ModelOption {
  id: string;
  label: string;
}

export interface ModelCatalog {
  openAi: ModelOption[];
  anthropic: ModelOption[];
  defaults: ModelSelection;
}

export interface AppConfigResponse {
  executionMode: string;
  speechEnabled: boolean;
  speechVoice: string;
  providers: ProviderAvailability;
  models: ModelCatalog;
  connectors: ConnectorDefinition[];
}

export interface SpeechTokenResponse {
  token: string;
  region: string;
  voice: string;
}

export interface ConnectorDefinition {
  id: string;
  label: string;
  kind: string;
  description: string;
  authMode: string;
  capabilities: string[];
  setupSummary: string;
  enabled: boolean;
}

export interface ConnectorRepositoryReference {
  connectorId: string;
  owner: string;
  repo: string;
  defaultBranch: string;
  private: boolean;
  description: string;
  url: string;
}

export interface ConnectorBranchReference {
  connectorId: string;
  owner: string;
  repo: string;
  name: string;
  isDefault: boolean;
  isProtected: boolean;
}

export interface ConnectorStatusResponse {
  id: string;
  label: string;
  enabled: boolean;
  status: string;
  authMode: string;
  repositoryCount: number;
  message: string;
}
