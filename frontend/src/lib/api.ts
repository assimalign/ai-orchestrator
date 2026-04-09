import { getAccessToken } from "../features/auth/auth-client";
import { runtimeConfig } from "./runtime-config";
import type {
  AppConfigResponse,
  ConversationInput,
  ConversationThread,
  ConversationThreadDetail,
  RepositoryTarget,
  SpeechTokenResponse,
} from "./models";

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const accessToken = await getAccessToken();
  if (!accessToken) {
    throw new Error("Sign in is required before making API calls.");
  }

  const headers = new Headers(init?.headers);
  headers.set("Authorization", `Bearer ${accessToken}`);
  const controller = new AbortController();
  const timeout = window.setTimeout(() => controller.abort(), 12000);

  try {
    const response = await fetch(`${runtimeConfig.apiBaseUrl}${path}`, {
      ...init,
      headers,
      signal: controller.signal,
    });

    if (!response.ok) {
      const body = await response.text();
      throw new Error(body || `Request failed with ${response.status}.`);
    }

    return (await response.json()) as T;
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError") {
      throw new Error("The API request timed out. Check whether the API container is healthy.");
    }

    throw error;
  } finally {
    window.clearTimeout(timeout);
  }
}

export function getConfig() {
  return request<AppConfigResponse>("/api/config");
}

export function listThreads() {
  return request<ConversationThread[]>("/api/threads");
}

export function getThread(threadId: string) {
  return request<ConversationThreadDetail>(`/api/threads/${threadId}`);
}

export function createThread(input: ConversationInput) {
  return request<ConversationThreadDetail>("/api/threads", {
    body: JSON.stringify(input),
    headers: {
      "Content-Type": "application/json",
    },
    method: "POST",
  });
}

export function postThreadMessage(threadId: string, input: ConversationInput) {
  return request<ConversationThreadDetail>(`/api/threads/${threadId}/messages`, {
    body: JSON.stringify(input),
    headers: {
      "Content-Type": "application/json",
    },
    method: "POST",
  });
}

export function promoteThread(threadId: string) {
  return request<ConversationThreadDetail>(`/api/threads/${threadId}/promote`, {
    method: "POST",
  });
}

export function getSpeechToken() {
  return request<SpeechTokenResponse>("/api/speech/token", {
    method: "POST",
  });
}

export function buildRepositoryTarget(
  owner: string,
  repo: string,
  baseBranch: string,
  targetBranch: string,
): RepositoryTarget | undefined {
  if (!owner.trim() || !repo.trim()) {
    return undefined;
  }

  return {
    owner: owner.trim(),
    repo: repo.trim(),
    baseBranch: baseBranch.trim() || undefined,
    targetBranch: targetBranch.trim() || undefined,
  };
}
