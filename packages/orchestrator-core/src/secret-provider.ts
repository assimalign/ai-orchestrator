import {
  AzureCliCredential,
  AzureDeveloperCliCredential,
  ChainedTokenCredential,
  ManagedIdentityCredential,
} from "@azure/identity";
import { SecretClient } from "@azure/keyvault-secrets";

export class SecretProvider {
  private client?: SecretClient;
  private cache = new Map<string, string>();
  private readonly credential = new ChainedTokenCredential(
    new ManagedIdentityCredential(),
    new AzureDeveloperCliCredential(),
    new AzureCliCredential(),
  );

  constructor(private readonly keyVaultUrl?: string) {}

  async get(name: string, directValue?: string): Promise<string | undefined> {
    if (directValue) {
      return directValue;
    }

    if (this.cache.has(name)) {
      return this.cache.get(name);
    }

    if (!this.keyVaultUrl) {
      return undefined;
    }

    if (!this.client) {
      this.client = new SecretClient(this.keyVaultUrl, this.credential);
    }

    let secret;
    try {
      secret = await this.client.getSecret(name);
    } catch (error) {
      const statusCode = (error as { statusCode?: number }).statusCode;
      const code = (error as { code?: string }).code;

      if (statusCode === 404 || code === "SecretNotFound") {
        return undefined;
      }

      throw error;
    }

    if (secret.value) {
      this.cache.set(name, secret.value);
    }

    return secret.value;
  }
}
