function pickValue(...values: Array<string | undefined>) {
  return values.find((value) => typeof value === "string" && value.trim().length > 0);
}

export const authCallbackPath = "/auth/callback";

export const runtimeConfig = {
  apiBaseUrl:
    pickValue(
      window.__APP_CONFIG__?.apiBaseUrl,
      import.meta.env.VITE_API_BASE_URL,
      "http://localhost:8080",
    ) ?? "http://localhost:8080",
  speechVoice:
    pickValue(
      window.__APP_CONFIG__?.speechVoice,
      import.meta.env.VITE_SPEECH_VOICE,
      "en-US-JennyNeural",
    ) ?? "en-US-JennyNeural",
  entraTenantId:
    pickValue(
      window.__APP_CONFIG__?.entraTenantId,
      import.meta.env.VITE_ENTRA_TENANT_ID,
      "",
    ) ?? "",
  entraClientId:
    pickValue(
      window.__APP_CONFIG__?.entraClientId,
      import.meta.env.VITE_ENTRA_CLIENT_ID,
      "",
    ) ?? "",
  entraScope:
    pickValue(
      window.__APP_CONFIG__?.entraScope,
      import.meta.env.VITE_ENTRA_SCOPE,
      "",
    ) ?? "",
};

export function getAuthRedirectUri() {
  return new URL(authCallbackPath, window.location.origin).toString();
}

export function getMissingRequiredConfiguration() {
  const missing: string[] = [];

  if (!runtimeConfig.entraTenantId) {
    missing.push("ENTRA_TENANT_ID");
  }

  if (!runtimeConfig.entraClientId) {
    missing.push("ENTRA_CLIENT_ID");
  }

  if (!runtimeConfig.apiBaseUrl) {
    missing.push("API_BASE_URL");
  }

  return missing;
}
