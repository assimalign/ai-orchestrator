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
    private readonly SecretClientOptions clientOptions = new()
    {
        Retry =
        {
            Delay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(2),
            MaxRetries = 2,
            NetworkTimeout = TimeSpan.FromSeconds(5),
        },
    };

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

        client ??= new SecretClient(new Uri(keyVaultUrl), credential, clientOptions);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(8));

        try
        {
            var response = await client.GetSecretAsync(secretName, cancellationToken: timeoutCts.Token);
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
        catch (Azure.RequestFailedException error) when (error.Status == 401 || error.Status == 403)
        {
            Console.Error.WriteLine($"Key Vault access denied while reading secret '{secretName}'.");
            return null;
        }
        catch (CredentialUnavailableException)
        {
            Console.Error.WriteLine($"No Azure credential was available while reading secret '{secretName}'.");
            return null;
        }
        catch (AuthenticationFailedException)
        {
            Console.Error.WriteLine($"Azure authentication failed while reading secret '{secretName}'.");
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Console.Error.WriteLine($"Key Vault lookup for secret '{secretName}' timed out.");
            return null;
        }
    }
}
