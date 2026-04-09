import { useAuth } from "../../features/auth/auth-provider";

export function AuthCallbackPage() {
  const { error, statusMessage } = useAuth();

  return (
    <div className="flex min-h-screen items-center justify-center px-6 py-10">
      <div className="w-full max-w-xl rounded-[2rem] border border-white/10 bg-white/5 p-8 shadow-panel backdrop-blur">
        <p className="text-xs font-semibold uppercase tracking-[0.32em] text-sage-300">
          Microsoft Callback
        </p>
        <h1 className="mt-3 text-3xl font-semibold tracking-tight text-white">
          Finishing sign-in
        </h1>
        <p className="mt-4 text-sm leading-7 text-slate-300">{statusMessage}</p>

        <div className="mt-8 rounded-3xl border border-white/10 bg-ink-900/75 p-5">
          <div className="flex items-center gap-3">
            <span className="h-3 w-3 animate-pulse rounded-full bg-sage-300" />
            <p className="text-sm font-medium text-white">
              {error ? "There was a problem completing the redirect." : "Waiting for the Microsoft response to settle."}
            </p>
          </div>
          {error ? (
            <p className="mt-3 text-sm text-rose-200">{error}</p>
          ) : (
            <p className="mt-3 text-sm text-slate-400">
              You should land back in the workspace automatically once the token is processed.
            </p>
          )}
        </div>
      </div>
    </div>
  );
}
