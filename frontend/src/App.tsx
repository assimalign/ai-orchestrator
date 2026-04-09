import { useEffect } from "react";
import { ConfigurationAlert } from "./components/app/ConfigurationAlert";
import { AuthCallbackPage } from "./components/auth/AuthCallbackPage";
import { useAuth } from "./features/auth/auth-provider";
import { WorkspacePage } from "./features/workspace/WorkspacePage";
import {
  authCallbackPath,
  getMissingRequiredConfiguration,
} from "./lib/runtime-config";

export function App() {
  const auth = useAuth();
  const missingConfiguration = getMissingRequiredConfiguration();

  useEffect(() => {
    if (window.location.pathname === authCallbackPath) {
      return;
    }

    if (!auth.ready || auth.isAuthenticated || auth.isSigningIn) {
      return;
    }

    if (missingConfiguration.length > 0 || auth.error) {
      return;
    }

    void auth.signIn();
  }, [
    auth.error,
    auth.isAuthenticated,
    auth.isSigningIn,
    auth.ready,
    auth.signIn,
    missingConfiguration.length,
  ]);

  if (window.location.pathname === authCallbackPath) {
    return <AuthCallbackPage />;
  }

  if (missingConfiguration.length > 0) {
    return <ConfigurationAlert missingKeys={missingConfiguration} />;
  }

  if (!auth.ready) {
    return (
      <div className="flex min-h-screen items-center justify-center px-6 py-10">
        <div className="w-full max-w-lg rounded-[2rem] border border-white/10 bg-white/5 p-8 text-center shadow-panel backdrop-blur">
          <p className="text-[11px] font-semibold uppercase tracking-[0.32em] text-sage-300">
            Loading
          </p>
          <h1 className="mt-3 text-3xl font-semibold tracking-tight text-white">
            Preparing your workspace
          </h1>
          <p className="mt-4 text-sm leading-7 text-slate-300">{auth.statusMessage}</p>
        </div>
      </div>
    );
  }

  if (auth.error) {
    return (
      <ConfigurationAlert
        missingKeys={[]}
        title="Authentication could not start"
        details={auth.error}
      />
    );
  }

  if (!auth.isAuthenticated) {
    return (
      <div className="flex min-h-screen items-center justify-center px-6 py-10">
        <div className="w-full max-w-lg rounded-[2rem] border border-white/10 bg-white/5 p-8 text-center shadow-panel backdrop-blur">
          <p className="text-[11px] font-semibold uppercase tracking-[0.32em] text-sage-300">
            Authentication
          </p>
          <h1 className="mt-3 text-3xl font-semibold tracking-tight text-white">
            Redirecting to Microsoft
          </h1>
          <p className="mt-4 text-sm leading-7 text-slate-300">
            {auth.statusMessage}
          </p>
        </div>
      </div>
    );
  }

  return <WorkspacePage />;
}
