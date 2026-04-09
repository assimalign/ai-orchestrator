using Assimalign.AI.Orchestrator.Application.Abstractions.Providers;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace Assimalign.AI.Orchestrator.Infrastructure.Security;

public sealed class SecretProvider(string? keyVaultUrl) : ISecretProvider
{
    private readonly Dictionary<string, string> cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ChainedTokenCredential credential = new(
        new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned),
        new AzureDeveloperCliCredential(),
        new AzureCliCredential());

    private SecretClient? client;

    public async Task<string?> GetAsync(
        string secretName,
        string? directValue = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(directValue))
        {
            return directValue;
        }

        if (cache.TryGetValue(secretName, out var cached))
        {
            return cached;
        }

        if (string.IsNullOrWhiteSpace(keyVaultUrl))
        {
            return null;
        }

        client ??= new SecretClient(new Uri(keyVaultUrl), credential);

        try
        {
            var response = await client.GetSecretAsync(secretName, cancellationToken: cancellationToken);
            if (!string.IsNullOrWhiteSpace(response.Value.Value))
            {
                cache[secretName] = response.Value.Value;
            }

            return response.Value.Value;
        }
        catch (Azure.RequestFailedException error) when (error.Status == 404 || error.ErrorCode == "SecretNotFound")
        {
            return null;
        }
    }
}
