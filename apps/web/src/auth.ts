import {
  type AccountInfo,
  PublicClientApplication,
  InteractionRequiredAuthError,
} from "@azure/msal-browser";

const config = {
  apiBaseUrl: window.__APP_CONFIG__?.apiBaseUrl ?? "http://localhost:8080",
  authEnabled: Boolean(window.__APP_CONFIG__?.authEnabled),
  entraTenantId: window.__APP_CONFIG__?.entraTenantId ?? "",
  entraClientId: window.__APP_CONFIG__?.entraClientId ?? "",
  entraScope: window.__APP_CONFIG__?.entraScope ?? "",
};

export const webRuntimeConfig = config;

const msalApp =
  config.authEnabled && config.entraTenantId && config.entraClientId
    ? new PublicClientApplication({
        auth: {
          authority: `https://login.microsoftonline.com/${config.entraTenantId}`,
          clientId: config.entraClientId,
          postLogoutRedirectUri: window.location.origin,
          redirectUri: window.location.origin,
        },
        cache: {
          cacheLocation: "sessionStorage",
        },
      })
    : undefined;

function getScopes() {
  const apiScope =
    config.entraScope || `api://${config.entraClientId}/access_as_user`;

  return ["openid", "profile", "offline_access", apiScope];
}

export async function initializeAuth() {
  if (!msalApp) {
    return {
      account: undefined,
      enabled: false,
    };
  }

  await msalApp.initialize();

  const existingAccount = msalApp.getActiveAccount() ?? msalApp.getAllAccounts()[0];
  if (existingAccount) {
    msalApp.setActiveAccount(existingAccount);
  }

  return {
    account: existingAccount,
    enabled: true,
  };
}

export async function signIn() {
  if (!msalApp) {
    throw new Error("Microsoft Entra auth is not configured for this app.");
  }

  const response = await msalApp.loginPopup({
    prompt: "select_account",
    scopes: getScopes(),
  });

  msalApp.setActiveAccount(response.account);
  return response.account;
}

export async function signOut() {
  if (!msalApp) {
    return;
  }

  await msalApp.logoutPopup({
    account: msalApp.getActiveAccount() ?? undefined,
    mainWindowRedirectUri: window.location.origin,
  });
}

export async function getAccessToken() {
  if (!msalApp) {
    return undefined;
  }

  const account = msalApp.getActiveAccount() ?? msalApp.getAllAccounts()[0];
  if (!account) {
    return undefined;
  }

  try {
    const response = await msalApp.acquireTokenSilent({
      account,
      scopes: getScopes(),
    });

    return response.accessToken;
  } catch (error) {
    if (!(error instanceof InteractionRequiredAuthError)) {
      throw error;
    }

    const response = await msalApp.acquireTokenPopup({
      account,
      scopes: getScopes(),
    });

    return response.accessToken;
  }
}

export function getSignedInAccount(): AccountInfo | undefined {
  if (!msalApp) {
    return undefined;
  }

  return msalApp.getActiveAccount() ?? msalApp.getAllAccounts()[0];
}
