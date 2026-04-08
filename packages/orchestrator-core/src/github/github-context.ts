import { App } from "@octokit/app";
import { Octokit } from "octokit";
import type { GitHubContextSnapshot, RepositoryTarget } from "@ai-dev-orchestrator/shared";

export interface GitHubClientConfig {
  appId?: string;
  privateKey?: string;
  installationId?: string;
  token?: string;
}

export async function createGitHubClient(
  config: GitHubClientConfig,
): Promise<Octokit | undefined> {
  if (config.token) {
    return new Octokit({ auth: config.token });
  }

  if (config.appId && config.privateKey && config.installationId) {
    const app = new App({
      appId: config.appId,
      privateKey: config.privateKey,
    });

    return app.getInstallationOctokit(Number(config.installationId));
  }

  return undefined;
}

export class GitHubContextService {
  constructor(private readonly client?: Octokit) {}

  async listRepositories() {
    if (!this.client) {
      return [];
    }

    try {
      const response = await this.client.request("GET /installation/repositories", {
        per_page: 100,
      });

      return response.data.repositories.map((repository: {
        id: number;
        owner: { login: string };
        name: string;
        default_branch: string;
        private: boolean;
        description: string | null;
        html_url: string;
      }) => ({
        id: repository.id,
        owner: repository.owner.login,
        repo: repository.name,
        defaultBranch: repository.default_branch,
        private: repository.private,
        description: repository.description ?? "",
        url: repository.html_url,
      }));
    } catch {
      const response = await this.client.request("GET /user/repos", {
        per_page: 100,
        sort: "updated",
      });

      return response.data.map((repository: {
        id: number;
        owner: { login: string };
        name: string;
        default_branch: string;
        private: boolean;
        description: string | null;
        html_url: string;
      }) => ({
        id: repository.id,
        owner: repository.owner.login,
        repo: repository.name,
        defaultBranch: repository.default_branch,
        private: repository.private,
        description: repository.description ?? "",
        url: repository.html_url,
      }));
    }
  }

  async buildSnapshot(
    target?: RepositoryTarget,
  ): Promise<GitHubContextSnapshot | undefined> {
    if (!target) {
      return undefined;
    }

    const snapshot: GitHubContextSnapshot = {
      repository: {
        owner: target.owner,
        repo: target.repo,
      },
      notes: [],
    };

    if (!this.client) {
      snapshot.notes.push("GitHub credentials are not configured, so only the requested repository coordinates were included.");
      return snapshot;
    }

    const repositoryResponse = await this.client.request("GET /repos/{owner}/{repo}", {
      owner: target.owner,
      repo: target.repo,
    });

    snapshot.repository = {
      owner: target.owner,
      repo: target.repo,
      defaultBranch: repositoryResponse.data.default_branch,
      description: repositoryResponse.data.description ?? "",
      url: repositoryResponse.data.html_url,
    };

    if (target.issueNumber) {
      const issueResponse = await this.client.request(
        "GET /repos/{owner}/{repo}/issues/{issue_number}",
        {
          owner: target.owner,
          repo: target.repo,
          issue_number: target.issueNumber,
        },
      );

      snapshot.issue = {
        number: issueResponse.data.number,
        title: issueResponse.data.title,
        body: issueResponse.data.body ?? "",
        labels: issueResponse.data.labels.map((label: string | { name?: string | null }) =>
          typeof label === "string" ? label : label.name ?? "",
        ),
        state: issueResponse.data.state,
        url: issueResponse.data.html_url,
      };
    }

    if (target.pullRequestNumber) {
      const pullRequestResponse = await this.client.request(
        "GET /repos/{owner}/{repo}/pulls/{pull_number}",
        {
          owner: target.owner,
          repo: target.repo,
          pull_number: target.pullRequestNumber,
        },
      );

      snapshot.pullRequest = {
        number: pullRequestResponse.data.number,
        title: pullRequestResponse.data.title,
        body: pullRequestResponse.data.body ?? "",
        state: pullRequestResponse.data.state,
        url: pullRequestResponse.data.html_url,
      };
    }

    return snapshot;
  }
}
