export type Provider = "openai" | "anthropic";

export type RunStatus =
  | "queued"
  | "planning"
  | "reviewing"
  | "synthesizing"
  | "completed"
  | "failed";

export interface RepositoryTarget {
  owner: string;
  repo: string;
  branch?: string;
  issueNumber?: number;
  pullRequestNumber?: number;
}

export interface ConversationInput {
  text: string;
  repository?: RepositoryTarget;
}

export interface RunArtifact {
  id: string;
  stage: RunStatus | "input";
  title: string;
  content: string;
  provider?: Provider;
  createdAt: string;
  metadata?: Record<string, string | number | boolean>;
}

export interface OrchestrationRun {
  id: string;
  status: RunStatus;
  createdAt: string;
  updatedAt: string;
  input: ConversationInput;
  artifacts: RunArtifact[];
  summary?: string;
  error?: string;
}

export interface GitHubIssueSnapshot {
  number: number;
  title: string;
  body: string;
  labels: string[];
  state: string;
  url: string;
}

export interface GitHubPullRequestSnapshot {
  number: number;
  title: string;
  body: string;
  url: string;
  state: string;
}

export interface GitHubContextSnapshot {
  repository: {
    owner: string;
    repo: string;
    defaultBranch?: string;
    description?: string;
    url?: string;
  };
  issue?: GitHubIssueSnapshot;
  pullRequest?: GitHubPullRequestSnapshot;
  notes: string[];
}

export interface PlanningArtifact {
  objective: string;
  workstreams: string[];
  risks: string[];
  firstTasks: string[];
  suggestedBranchName?: string;
}

export interface ReviewArtifact {
  concerns: string[];
  missingContext: string[];
  improvements: string[];
}

export interface RunResult {
  context?: GitHubContextSnapshot;
  plan: PlanningArtifact;
  review: ReviewArtifact;
  summary: string;
  nextActions: string[];
}
