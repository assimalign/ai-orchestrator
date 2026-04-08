/// <reference types="vite/client" />

interface Window {
  __APP_CONFIG__?: {
    apiBaseUrl?: string;
    speechVoice?: string;
    authEnabled?: boolean;
    entraTenantId?: string;
    entraClientId?: string;
    entraScope?: string;
  };
}
