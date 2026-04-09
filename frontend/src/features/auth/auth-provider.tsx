import {
  createContext,
  useContext,
  useEffect,
  useMemo,
  useState,
  type PropsWithChildren,
} from "react";
import type { AccountInfo } from "@azure/msal-browser";
import {
  getSignedInAccount,
  initializeAuth,
  signIn as beginSignIn,
  signOut as beginSignOut,
} from "./auth-client";
import { getMissingRequiredConfiguration } from "../../lib/runtime-config";

type AuthContextValue = {
  account?: AccountInfo;
  accountLabel?: string;
  error?: string;
  isAuthenticated: boolean;
  isSigningIn: boolean;
  ready: boolean;
  statusMessage: string;
  signIn: () => Promise<void>;
  signOut: () => Promise<void>;
};

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: PropsWithChildren) {
  const [account, setAccount] = useState<AccountInfo>();
  const [statusMessage, setStatusMessage] = useState("Preparing your protected workspace.");
  const [ready, setReady] = useState(false);
  const [isSigningIn, setIsSigningIn] = useState(false);
  const [error, setError] = useState<string>();

  useEffect(() => {
    let active = true;

    async function boot() {
      const missingConfiguration = getMissingRequiredConfiguration();

      if (missingConfiguration.length > 0) {
        if (!active) {
          return;
        }

        const message = `Missing required configuration: ${missingConfiguration.join(", ")}.`;
        setError(message);
        setReady(true);
        setStatusMessage(message);
        return;
      }

      try {
        const result = await initializeAuth();
        if (!active) {
          return;
        }

        const nextAccount = result.account ?? getSignedInAccount();
        setAccount(nextAccount);
        setReady(true);
        setStatusMessage(
          nextAccount
            ? "Connected to your Microsoft Entra session."
            : result.isCallbackRoute
              ? "Finishing the Microsoft sign-in flow."
              : "Sign in with Microsoft to access the orchestrator.",
        );
      } catch (bootError) {
        if (!active) {
          return;
        }

        const message =
          bootError instanceof Error ? bootError.message : "Authentication failed.";

        setError(message);
        setReady(true);
        setStatusMessage(message);
      }
    }

    void boot();

    return () => {
      active = false;
    };
  }, []);

  async function signIn() {
    setError(undefined);
    setIsSigningIn(true);
    setStatusMessage("Redirecting to Microsoft sign-in.");

    try {
      await beginSignIn();
    } catch (signInError) {
      const message =
        signInError instanceof Error ? signInError.message : "Sign-in failed.";

      setError(message);
      setIsSigningIn(false);
      setStatusMessage(message);
    }
  }

  async function signOut() {
    setStatusMessage("Signing out.");
    await beginSignOut();
  }

  const value = useMemo<AuthContextValue>(
    () => ({
      account,
      accountLabel: account?.name ?? account?.username,
      error,
      isAuthenticated: Boolean(account),
      isSigningIn,
      ready,
      signIn,
      signOut,
      statusMessage,
    }),
    [account, error, isSigningIn, ready, statusMessage],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within an AuthProvider.");
  }

  return context;
}
