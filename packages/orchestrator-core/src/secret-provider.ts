import { DefaultAzureCredential } from "@azure/identity";
import { SecretClient } from "@azure/keyvault-secrets";

export class SecretProvider {
  private client?: SecretClient;
  private cache = new Map<string, string>();

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
      this.client = new SecretClient(this.keyVaultUrl, new DefaultAzureCredential());
    }

    const secret = await this.client.getSecret(name);
    if (secret.value) {
      this.cache.set(name, secret.value);
    }

    return secret.value;
  }
}
