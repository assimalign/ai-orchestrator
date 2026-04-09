namespace Assimalign.AI.Orchestrator.Application.Runtime;

public sealed class OrchestratorRuntimeHandle(Func<CancellationToken, Task<OrchestratorRuntime>> initializeAsync)
{
    private readonly Lazy<Task<OrchestratorRuntime>> runtimeTask = new(
        () => initializeAsync(CancellationToken.None),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public void EnsureStarted() => _ = runtimeTask.Value;

    public string State =>
        !runtimeTask.IsValueCreated
            ? "notStarted"
            : runtimeTask.Value.IsCompletedSuccessfully
                ? "ready"
                : runtimeTask.Value.IsFaulted
                    ? "failed"
                    : runtimeTask.Value.IsCanceled
                        ? "canceled"
                        : "initializing";

    public string? ErrorMessage =>
        runtimeTask.IsValueCreated && runtimeTask.Value.IsFaulted
            ? runtimeTask.Value.Exception?.GetBaseException().Message
            : null;

    public async Task<OrchestratorRuntime> GetRuntimeAsync(
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(10);
        return await runtimeTask.Value.WaitAsync(effectiveTimeout, cancellationToken);
    }
}
