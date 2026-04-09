# AI Dev Orchestrator

`ai-dev-orchestrator` is a GitHub-centered Azure deployment for a protected, thread-based development workspace. The frontend keeps long-lived conversations like Codex and Claude, exposes connector-backed repository selection, and the backend runs a staged flow where Codex plans, Claude critiques, Codex synthesizes the final response, and implementation requests can clone the attached repository, edit files, run verification, commit, and push onto the thread's working branch.

## Repo layout

- `frontend`: Vite + React UI with Microsoft Entra sign-in, a Codex-style thread sidebar, repository branch workflow controls, voice capture, and compact stage cards for Codex/Claude handoffs
- `backend/services/Assimalign.AI.Orchestrator.Api`: ASP.NET Core API host
- `backend/services/Assimalign.AI.Orchestrator.Worker`: .NET worker host for background processing
- `backend/core/Assimalign.AI.Orchestrator.Core`: public-facing models, prompts, and shared core resources
- `backend/application/Assimalign.AI.Orchestrator.Application`: orchestration use cases, configuration, and host-facing security/platform wiring
- `backend/infrastructure/Assimalign.AI.Orchestrator.Infrastructure*`: infrastructure abstractions plus concrete implementations for messaging, storage, and external integrations
- `Assimalign.AI.Orchestrator.slnx`: root solution for the full backend and frontend verification flow

Infrastructure projects follow an abstraction-first pattern. For example:

- `backend/infrastructure/Assimalign.AI.Orchestrator.Infrastructure.Messaging`: messaging contracts
- `backend/infrastructure/Assimalign.AI.Orchestrator.Infrastructure.Messaging.ServiceBus`: Service Bus messaging implementation
- `backend/infrastructure/Assimalign.AI.Orchestrator.Infrastructure.Storage`: storage contracts
- `backend/infrastructure/Assimalign.AI.Orchestrator.Infrastructure.Storage.Memory`: in-memory storage implementation
- `backend/infrastructure/Assimalign.AI.Orchestrator.Infrastructure.Storage.Tables`: Azure Tables storage implementation
- `infra`: Azure Container Apps, Storage, Service Bus, Speech, Log Analytics, App Insights, and Key Vault infrastructure

The connector model starts with GitHub, but the API and UI now treat it as a connector surface so additional repo-capable plugins can be added later without reworking the thread model.

## Local development

1. Copy `.env.example` to `.env` for the backend.
2. Copy `frontend/.env.example` to `frontend/.env.local` for the frontend.
3. Set `EXECUTION_MODE=inline` if you want to run without Service Bus locally.
4. Provide `ENTRA_TENANT_ID`, `ENTRA_CLIENT_ID`, and your model keys.
5. Set `APPLICATIONINSIGHTS_CONNECTION_STRING` and `VITE_APPLICATIONINSIGHTS_CONNECTION_STRING` if you want telemetry during local development.
6. Install frontend dependencies:

```bash
npm install --prefix frontend
```

7. Run the API:

```bash
npm run api:dev
```

8. Run the frontend:

```bash
npm run frontend:dev
```

9. If you want queue-backed processing, run the worker too:

```bash
npm run worker:dev
```

The default Vite dev URL is `http://localhost:5173`.

Execution-related backend settings:

- `REPOSITORY_WORKSPACE_ROOT`: optional temp workspace root for cloned repositories
- `REPOSITORY_COMMAND_TIMEOUT_SECONDS`: per-command timeout for setup/test/git operations
- `GIT_COMMIT_USER_NAME`: git author name used for worker-created commits
- `GIT_COMMIT_USER_EMAIL`: git author email used for worker-created commits

## GitHub configuration

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

If you want the orchestrator to prepare working branches, commit code, push changes, and promote them upstream, the GitHub token or GitHub App needs repository `Contents: write` permission.

The deploy workflow always enables Microsoft Entra auth and reuses:

- `AZURE_TENANT_ID`
- `AZURE_CLIENT_ID`

## Deployment notes

- The GitHub Actions deploy workflow builds public GHCR images and points Container Apps at those image tags.
- The API and worker use managed identity in Azure and the signed-in Azure developer identity locally when accessing Key Vault.
- The thread state is stored in Azure Table Storage and the worker queue runs through Azure Service Bus.
- The worker container now needs `git` plus a usable build/test toolchain. The provided worker image includes the .NET SDK, `git`, `node`, `npm`, `python3`, `pip3`, `curl`, `jq`, and common archive/build utilities so typical repos can bootstrap more tooling during setup.

## Repository execution flow

1. Attach a GitHub repo, base branch, and target branch in the thread.
2. Send an implementation request.
3. Codex drafts the approach, Claude critiques it, and Codex makes the final implementation decision.
4. The worker prepares the thread's working branch on GitHub.
5. The worker clones that repo/branch into an isolated temp workspace, asks Codex for concrete file edits and verification commands, applies the edits, runs setup/tool-install commands as needed, then runs tests, commits, and pushes back to the working branch.
6. Review the pushed changes in GitHub through the compare URL shown in the thread, or locally with `git fetch` and `git checkout <working-branch>`.
7. When you're happy with the branch, use the thread's promote action to merge the working branch into the target branch.
