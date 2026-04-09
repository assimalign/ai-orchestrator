type ConfigurationAlertProps = {
  details?: string;
  missingKeys: string[];
  title?: string;
};

export function ConfigurationAlert({
  details,
  missingKeys,
  title = "Missing required configuration",
}: ConfigurationAlertProps) {
  return (
    <div className="flex min-h-screen items-center justify-center px-6 py-10">
      <div
        className="w-full max-w-3xl rounded-[2rem] border border-rose-400/25 bg-rose-500/10 p-8 shadow-panel backdrop-blur"
        role="alert"
      >
        <p className="text-[11px] font-semibold uppercase tracking-[0.32em] text-rose-200">
          Configuration Alert
        </p>
        <h1 className="mt-3 text-3xl font-semibold tracking-tight text-white">
          {title}
        </h1>
        <p className="mt-4 text-sm leading-7 text-rose-100/90">
          {details ??
            "The application cannot complete authentication or call the backend until the required runtime configuration is present."}
        </p>

        {missingKeys.length > 0 ? (
          <div className="mt-6 rounded-[1.5rem] border border-white/10 bg-black/25 p-5">
            <p className="text-xs font-semibold uppercase tracking-[0.26em] text-rose-200">
              Missing keys
            </p>
            <div className="mt-4 flex flex-wrap gap-3">
              {missingKeys.map((key) => (
                <span
                  key={key}
                  className="rounded-full border border-rose-300/20 bg-rose-400/10 px-3 py-1.5 text-xs font-medium tracking-[0.16em] text-rose-100"
                >
                  {key}
                </span>
              ))}
            </div>
          </div>
        ) : null}
      </div>
    </div>
  );
}
