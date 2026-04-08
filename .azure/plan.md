# Azure Deployment Plan

> **Status:** Ready for Validation

Generated: 2026-04-08T00:00:00-04:00

---

## 1. Project Overview

**Goal:** Build a cloud-hosted multi-model development orchestrator that can coordinate OpenAI/Codex and Claude over shared GitHub-centric workflows, keep durable task state for long-running work, and provide a voice-enabled conversational UI so requirements can be spoken as well as typed.

**Path:** New Project

**Proposed repository:** `C:\Source\repos\assimalign\ai-dev-orchestrator`

---

## 2. Requirements

| Attribute | Value |
|-----------|-------|
| Classification | Development, with a production-ready architecture path |
| Scale | Medium |
| Budget | Balanced |
| **Subscription** | User to confirm. Azure CLI is not installed locally, so subscription could not be auto-detected. |
| **Location** | `eastus2` recommended, pending deployment-time confirmation |

**Working assumptions to confirm with user:**
- Internal engineering platform rather than a public SaaS product
- Single-region deployment is acceptable for v1
- GitHub will be the system of record for repos, issues, pull requests, and automation context
- OpenAI and Anthropic API credentials will be provided separately from ChatGPT / Claude app subscriptions
- Container Apps will pull public images from GitHub Container Registry instead of Azure Container Registry

---

## 3. Components Detected

| Component | Type | Technology | Path |
|-----------|------|------------|------|
| Workspace root | Existing parent folder | Mixed repositories | `C:\Source\repos\assimalign` |
| `ai-dev-orchestrator` | New project folder | To be scaffolded | `C:\Source\repos\assimalign\ai-dev-orchestrator` |

**Local tool observations:**
- `git` is installed
- `node` is installed
- `az` is not installed
- `azd` is not installed
- `pnpm` is not installed
- `git` repository initialized locally for the new project

---

## 4. Recipe Selection

**Selected:** Bicep

**Rationale:**
- User explicitly asked for Azure infrastructure via Bicep
- A direct GitHub Actions deployment pipeline fits the requested GitHub-centric workflow
- Local `azd` is unavailable, so choosing a plain Bicep recipe avoids unnecessary local tool coupling
- The orchestrator is a multi-service app, but direct Bicep keeps the infrastructure definition transparent and reviewable

---

## 5. Architecture

**Stack:** Containers

### Application Shape

- `apps/web`: React-based conversational UI with microphone capture, transcript display, and task/run dashboards
- `apps/api`: HTTP API for conversations, speech token issuance, orchestration control, GitHub webhook handling, and provider routing
- `apps/worker`: Background executor for long-running planning, implementation, review, and remediation jobs
- `packages/shared`: Shared types, config parsing, provider contracts, prompt schemas, and orchestration state models

### Service Mapping

| Component | Azure Service | SKU |
|-----------|---------------|-----|
| Web frontend | Azure Container Apps | Consumption / workload profile default |
| API backend | Azure Container Apps | Consumption / workload profile default |
| Background worker | Azure Container Apps | Consumption / workload profile default |
| Container images | GitHub Container Registry public images | Managed by GitHub Actions |
| Durable workflow queue | Azure Service Bus | Standard |
| Durable state store | Azure Storage Tables | Standard LRS |
| Voice conversation (speech-to-text, text-to-speech) | Azure AI Speech | S0 |
| Artifacts / transcripts / exported run data | Azure Blob Storage | Standard LRS |

### Supporting Services

| Service | Purpose |
|---------|---------|
| Log Analytics | Centralized logs for Container Apps and diagnostics |
| Application Insights | Monitoring, traces, request telemetry, job telemetry |
| Key Vault | OpenAI, Anthropic, GitHub App, database, and speech secrets |
| Managed Identity | Azure resource access without embedded secrets |

### Orchestrator Behavior

- Primary interaction surface is GitHub: repos, pull requests, issues, and webhooks
- Voice interaction is browser-based, with Azure Speech used for transcription and speech synthesis support
- Long-running work is split into explicit stages such as `intake`, `plan`, `implement`, `review`, `repair`, and `publish`
- Provider roles are intentionally bounded:
  - OpenAI/Codex for implementation-heavy execution and coding tasks
  - Claude for critique, review, design alternatives, and secondary analysis
- Durable run state is stored in PostgreSQL, while asynchronous work dispatch flows through Service Bus

### Security Posture

- GitHub Actions deploys through Azure workload identity / OIDC rather than client secrets where possible
- Runtime secrets are referenced from Key Vault
- Container Apps use managed identity for Azure service access
- GitHub integration is designed around a GitHub App rather than a long-lived personal access token

---

## 6. Execution Checklist

### Phase 1: Planning
- [x] Analyze workspace
- [x] Gather requirements
- [ ] Confirm subscription and location with user
- [x] Scan codebase
- [x] Select recipe
- [x] Plan architecture
- [x] **User approved this plan**

### Phase 2: Execution
- [x] Research components (load references, invoke skills)
- [x] For other services: Generate infrastructure files following service-specific guidance
- [x] Generate application configuration
- [x] Generate Dockerfiles
- [x] Generate GitHub Actions CI/CD pipeline
- [x] Initialize the new git repository
- [x] Update plan status to "Ready for Validation"

### Phase 3: Validation
- [ ] Invoke azure-validate skill
- [ ] All validation checks pass
- [ ] Update plan status to "Validated"
- [ ] Record validation proof below

### Phase 4: Deployment
- [ ] Invoke azure-deploy skill
- [ ] Deployment successful
- [ ] Update plan status to "Deployed"

---

## 7. Validation Proof

> **Required after validation.**

| Check | Command Run | Result | Timestamp |
|-------|-------------|--------|-----------|
| Pending | Pending | Pending | Pending |

**Validated by:** Pending
**Validation timestamp:** Pending

---

## 8. Files to Generate

| File | Purpose | Status |
|------|---------|--------|
| `.azure/plan.md` | Plan and workflow source of truth | ✅ |
| `README.md` | Project overview and operator workflow | ✅ |
| `.gitignore` | Repo hygiene | ✅ |
| `package.json` | Workspace root configuration | ✅ |
| `tsconfig.base.json` | Shared TypeScript configuration | ✅ |
| `apps/web/*` | Voice-enabled conversation frontend | ✅ |
| `apps/api/*` | Orchestration API and provider routing | ✅ |
| `apps/worker/*` | Queue-driven orchestration worker | ✅ |
| `packages/shared/*` | Shared contracts and config | ✅ |
| `infra/main.bicep` | Core Azure resource graph | ✅ |
| `infra/modules/*` | Reusable infrastructure modules | ✅ |
| `infra/main.parameters.json` | Deployment parameters | ✅ |
| `.github/workflows/ci.yml` | Lint, typecheck, and test pipeline | ✅ |
| `.github/workflows/deploy.yml` | Azure build and deploy pipeline | ✅ |
| `docker/web.Dockerfile` | Web container image | ✅ |
| `docker/api.Dockerfile` | API container image | ✅ |
| `docker/worker.Dockerfile` | Worker container image | ✅ |

---

## 9. Next Steps

> Current: Implementation is complete locally and ready for Azure validation

1. Confirm the Azure subscription and final region for deployment
2. Run Azure validation and then trigger the GitHub Actions deployment workflow
3. Make the GHCR packages public if they are not already, then deploy Container Apps against those image tags
