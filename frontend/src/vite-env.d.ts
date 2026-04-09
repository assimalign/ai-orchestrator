/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL?: string;
  readonly VITE_ENTRA_TENANT_ID?: string;
  readonly VITE_ENTRA_CLIENT_ID?: string;
  readonly VITE_SPEECH_VOICE?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}

interface Window {
  __APP_CONFIG__?: {
    apiBaseUrl?: string;
    speechVoice?: string;
    entraTenantId?: string;
    entraClientId?: string;
  };
}
