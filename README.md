# AI Dev Orchestrator

`ai-dev-orchestrator` is a GitHub-centered Azure deployment that lets you speak a development request, route it through OpenAI and Claude, and keep durable run history while long-running work executes in the background.

## What is in this repo

- `apps/web`: React voice UI for requirement intake, run monitoring, and summary playback
- `apps/api`: Fastify API for speech token issuance, run creation, GitHub context lookup, and orchestration control
- `apps/worker`: Queue-driven background worker that executes the multi-model flow
- `packages/shared`: Shared run, artifact, and GitHub context contracts
- `packages/orchestrator-core`: Provider clients, GitHub integration, storage, queue, and run processor logic
- `infra`: Azure Container Apps, Storage, Service Bus, Speech, Log Analytics, App Insights, and Key Vault infrastructure
- `.github/workflows`: CI and deployment automation

## Orchestration flow

1. The web app captures typed or spoken requirements.
2. The API creates a run record and enqueues it.
3. The worker loads GitHub context, asks OpenAI to build a plan, asks Claude to critique it, and asks OpenAI to synthesize the final brief.
4. Run artifacts and status updates are stored in Azure Table Storage for the UI to poll.

## GHCR deployment model

Container Apps are configured to pull images from `ghcr.io`.

- The GitHub Actions deploy workflow builds three images and pushes them to GHCR.
- Azure Container Apps use the public GHCR image URLs, so they do not require registry credentials at runtime.
- You still need GitHub Actions package publishing permissions. The workflow uses the repository `GITHUB_TOKEN` with `packages: write`.
- After the first push, ensure the GHCR packages are public if they are not already.

## Required GitHub configuration

Set these repository secrets for deployment:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`
- `OPENAI_API_KEY`
- `ANTHROPIC_API_KEY`
- `AZURE_SPEECH_KEY`
- `ORCH_GITHUB_RUNTIME_TOKEN` or `ORCH_GITHUB_APP_PRIVATE_KEY`
- `ORCH_GITHUB_WEBHOOK_SECRET`

Set these repository variables:

- `AZURE_LOCATION`
- `AZURE_RESOURCE_GROUP`
- `AZURE_BASE_NAME`
- `ORCH_GITHUB_APP_ID` if using a GitHub App
- `ORCH_GITHUB_INSTALLATION_ID` if using a GitHub App

## Local development

1. Copy `.env.example` to `.env`
2. Set `EXECUTION_MODE=inline`
3. Provide `OPENAI_API_KEY` and optionally `ANTHROPIC_API_KEY`
4. Run `npm install`
5. Run the services in separate terminals:

```bash
npm run dev --workspace @ai-dev-orchestrator/api
npm run dev --workspace @ai-dev-orchestrator/web
```

For queue-backed local work, also configure Azure Storage and Service Bus connection strings plus the worker process:

```bash
npm run dev --workspace @ai-dev-orchestrator/worker
```

## Deployment notes

- The Bicep template creates Key Vault, but runtime model and GitHub secrets are seeded by the deploy workflow after infrastructure is up.
- The API and worker use managed identity to read secrets from Key Vault.
- The current API CORS setting defaults to `*` for easy first deployment. Tighten that once your public web URL is stable.
