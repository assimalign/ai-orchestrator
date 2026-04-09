namespace Assimalign.AI.Orchestrator.Application.Abstractions.Providers;

public interface ISecretProvider
{
    Task<string?> GetAsync(
        string secretName,
        string? directValue = null,
        CancellationToken cancellationToken = default);
}
