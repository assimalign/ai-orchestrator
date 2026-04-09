import {
  type AccountInfo,
  InteractionRequiredAuthError,
  PublicClientApplication,
} from "@azure/msal-browser";
import { authCallbackPath, getAuthRedirectUri, runtimeConfig } from "../../lib/runtime-config";

const returnUrlKey = "ai-dev-orchestrator:return-url";

let msalApp: PublicClientApplication | undefined;

function getClient() {
  if (!runtimeConfig.entraTenantId || !runtimeConfig.entraClientId) {
    throw new Error(
      "Microsoft Entra is not configured. Set ENTRA_TENANT_ID and ENTRA_CLIENT_ID.",
    );
  }

  msalApp ??= new PublicClientApplication({
    auth: {
      authority: `https://login.microsoftonline.com/${runtimeConfig.entraTenantId}`,
      clientId: runtimeConfig.entraClientId,
      redirectUri: getAuthRedirectUri(),
      postLogoutRedirectUri: window.location.origin,
      navigateToLoginRequestUrl: false,
    },
    cache: {
      cacheLocation: "sessionStorage",
    },
  });

  return msalApp;
}

function getLoginScopes() {
  return runtimeConfig.entraScope ? [runtimeConfig.entraScope] : ["openid", "profile"];
}

function getAccessScopes() {
  return runtimeConfig.entraScope ? [runtimeConfig.entraScope] : ["openid", "profile"];
}

function rememberReturnUrl() {
  const currentPath = `${window.location.pathname}${window.location.search}${window.location.hash}`;
  if (currentPath !== authCallbackPath) {
    window.sessionStorage.setItem(returnUrlKey, currentPath);
  }
}

function restoreReturnUrl() {
  const remembered = window.sessionStorage.getItem(returnUrlKey);
  window.sessionStorage.removeItem(returnUrlKey);

  return remembered && remembered.startsWith("/") ? remembered : "/";
}

export async function initializeAuth() {
  const client = getClient();
  await client.initialize();

  const redirectResult = await client.handleRedirectPromise();
  const account = redirectResult?.account ?? client.getActiveAccount() ?? client.getAllAccounts()[0];

  if (account) {
    client.setActiveAccount(account);
  }

  if (window.location.pathname === authCallbackPath && account) {
    window.history.replaceState({}, document.title, restoreReturnUrl());
  }

  return {
    account,
    isCallbackRoute: window.location.pathname === authCallbackPath,
  };
}

export async function signIn() {
  rememberReturnUrl();

  await getClient().loginRedirect({
    prompt: "select_account",
    scopes: getLoginScopes(),
    redirectUri: getAuthRedirectUri(),
  });
}

export async function signOut() {
  await getClient().logoutRedirect({
    account: getClient().getActiveAccount() ?? undefined,
    postLogoutRedirectUri: window.location.origin,
  });
}

export async function getAccessToken() {
  const client = getClient();
  const account = client.getActiveAccount() ?? client.getAllAccounts()[0];

  if (!account) {
    return undefined;
  }

  try {
    const response = await client.acquireTokenSilent({
      account,
      scopes: getAccessScopes(),
    });
    return response.accessToken;
  } catch (error) {
    if (!(error instanceof InteractionRequiredAuthError)) {
      throw error;
    }

    rememberReturnUrl();
    await client.acquireTokenRedirect({
      account,
      scopes: getAccessScopes(),
      redirectUri: getAuthRedirectUri(),
    });

    return undefined;
  }
}

export function getSignedInAccount(): AccountInfo | undefined {
  if (!msalApp) {
    return undefined;
  }

  return msalApp.getActiveAccount() ?? msalApp.getAllAccounts()[0];
}
