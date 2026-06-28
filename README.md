# RunPod Orchestrator

ASP.NET Core middleware that simulates **serverless GPU inference** on RunPod Pods. It automatically discovers or creates a managed pod, resumes it on demand, proxies OpenAI-compatible requests to vLLM, and stops the pod after idle timeout.

**Quick start with Docker:** see [GETTING_STARTED.md](GETTING_STARTED.md)

Portuguese documentation: [README.pt-BR.md](README.pt-BR.md)

## How it works

Each orchestrator instance manages **one pod** transparently:

1. List existing pods in your RunPod account
2. Reuse a running or stopped pod whose **name** matches `RunPod__PodName` (prefers GPUs from your priority list)
3. If none exists, terminate any leftover pods with that name, then deploy a new pod on the first available GPU
4. Layered vLLM readiness (`/health` + `/v1/models`), runtime revalidation, proxy retry on upstream failure, auto-stop when idle, and optional terminate after extended idle (saves container disk cost)

Health checks and inference proxy use the RunPod HTTP proxy: `https://{podId}-{port}.proxy.runpod.net` (default port `8000` via `VllmPort`).

API clients never provide a pod ID.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) for local development
- [Docker](https://www.docker.com/) for containerized deployment
- RunPod account with API key

## Docker image (recommended)

Pull the published image:

```bash
docker pull mrffilipe/runpod-orchestrator:latest
```

See [GETTING_STARTED.md](GETTING_STARTED.md) for a complete `docker-compose.yml` and `.env` example.

The root `docker-compose.yml` also includes **Open WebUI** on port `3000` for quick chat testing against the orchestrator.

### Build locally

```bash
docker build -f backend/Dockerfile -t runpod-orchestrator:latest .
```

## Local development

```powershell
cd backend/RunpodOrchestrator.API

$env:RunPod__ApiKey = "your-runpod-api-key"
$env:RunPod__PreferredGpuTypeIds__0 = "NVIDIA GeForce RTX 4090"

dotnet run
```

Swagger UI is available in Development at `/swagger`. Health check: `GET /health`

## Environment variables

Configuration uses the standard ASP.NET Core convention: `Section__Property` (double underscore). Defaults live in `appsettings.json`; override via environment or `.env` with Docker Compose.

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `RunPod__ApiKey` | Yes | — | RunPod API key. Use a secrets manager in production. Never commit. |
| `RunPod__PreferredGpuTypeIds__0`, `__1`, … | No | `NVIDIA GeForce RTX 4090` | GPU types in priority order (indexed list) |
| `RunPod__PodName` | No | `runpod-orchestrator` | Name for newly created pods |
| `RunPod__PodTemplateId` | No | — | RunPod template ID for deploy (template-first; overrides image/ports/disk when set) |
| `RunPod__PodImageName` | No | `vllm/vllm-openai:latest` | Docker image for new pods (ignored when `RunPod__PodTemplateId` is set) |
| `RunPod__PodPorts` | No | `8000/http` | Exposed ports for new pods |
| `RunPod__VolumeMountPath` | No | `/workspace` | Volume mount path inside pod |
| `RunPod__VolumeInGb` | No | `40` | Volume size (GB) for new pods |
| `RunPod__ContainerDiskInGb` | No | `40` | Container disk size (GB) |
| `RunPod__NetworkVolumeId` | No | — | Optional persistent network volume |
| `Logging__LogLevel__Default` | No | `Information` | Application log level |
| `RunPod__RequestTimeoutSeconds` | No | `30` | RunPod GraphQL client timeout |
| `RunPod__VllmHealthCheckPath` | No | `/health` | vLLM health check path |
| `RunPod__VllmHealthCheckRetries` | No | `12` | Max vLLM health check attempts |
| `RunPod__VllmHealthCheckRetryDelaySeconds` | No | `5` | Delay between health retries |
| `RunPod__VllmHealthCheckTimeoutSeconds` | No | `10` | vLLM health HTTP client timeout |
| `RunPod__VllmProxyTimeoutSeconds` | No | `600` | Inference proxy timeout |
| `RunPod__VllmDefaultModel` | No | `Qwen/Qwen3-8B` | Model ID for `GET /v1/models` and `MODEL_NAME` on new pod deploy |
| `RunPod__PodEnvironment__*` | No | — | Extra pod env vars at deploy (e.g. `RunPod__PodEnvironment__HF_TOKEN=...`) |
| `RunPod__PodPollingIntervalSeconds` | No | `5` | Pod runtime polling interval |
| `RunPod__PodStartupTimeoutSeconds` | No | `600` | Max pod startup wait time |
| `RunPod__PodResumeMaxRetries` | No | `3` | Resume retries with exponential backoff |
| `RunPod__PodResumeRetryBaseDelaySeconds` | No | `2` | Base delay for resume backoff |
| `RunPod__PodHealthRecoveryEnabled` | No | `true` | Stop+resume after vLLM health failure |
| `RunPod__VllmReadinessModelsPath` | No | `/v1/models` | Path for model readiness check at startup |
| `RunPod__VllmReadinessRequireModel` | No | `true` | Require primary model in `/v1/models` response |
| `RunPod__VllmRuntimeHealthTtlSeconds` | No | `300` | Revalidate vLLM health after this many seconds while Ready |
| `RunPod__VllmRuntimeHealthRetries` | No | `3` | Quick health retries during runtime revalidation |
| `RunPod__VllmRuntimeHealthRetryDelaySeconds` | No | `2` | Delay between runtime health retries |
| `RunPod__VllmProxyRetryOnFailure` | No | `true` | Retry inference once after connection/upstream 502/503/504 |
| `RunPod__PodIdleTimeoutMinutes` | No | `15` | Auto-stop after idle minutes |
| `RunPod__PodIdleCheckIntervalSeconds` | No | `60` | Idle monitor check interval |
| `RunPod__PodTerminateCheckIntervalMinutes` | No | `60` | How often to check for post-idle pod termination |
| `RunPod__PodTerminateAfterIdleEnabled` | No | `true` | Terminate all `RunPod__PodName` pods after idle timeout persists |

## API endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/v1/models` | Configured model list for OpenAI clients (static; no GPU wake-up) |
| `POST` | `/v1/chat/completions` | Proxy to vLLM (OpenAI-compatible) |
| `POST` | `/v1/runpod/ensure-ready` | Ensure managed pod is running and healthy |
| `GET` | `/v1/runpod/status` | Managed pod status from RunPod API |
| `GET` | `/v1/runpod/idle-status` | Idle monitor state |
| `POST` | `/v1/runpod/terminate-managed` | Terminate all pods named `RunPod__PodName` |
| `GET` | `/health` | Orchestrator health probe |

Diagnostic endpoints under `/v1/runpod/*` are for operations and debugging.

## CI/CD — Docker Hub publish

GitHub Actions workflow [`.github/workflows/docker-publish.yml`](.github/workflows/docker-publish.yml) builds and pushes `mrffilipe/runpod-orchestrator`.

**Required repository secret:**

| Secret | Description |
|--------|-------------|
| `DOCKERHUB_TOKEN` | Docker Hub access token for user `mrffilipe` (push permission) |

**Triggers:**

- Manual: Actions → *Docker publish* → *Run workflow*
- Tag push: `docker-v1.0.0` (semver tags applied to the image)

## Production notes

1. Store `RunPod__ApiKey` in a secrets manager (Azure Key Vault, AWS Secrets Manager, etc.)
2. Run on a low-cost VPS, RunPod CPU pod, or Kubernetes
3. Point clients at `POST /v1/chat/completions`
4. Tune `RunPod__PodIdleTimeoutMinutes` to control GPU cost
5. Use `GET /health` for load balancer and container probes
6. Set `ASPNETCORE_ENVIRONMENT=Production` (Swagger disabled)

## Security

- RunPod API key is read from environment only — never hardcode it
- Key is sent via `Authorization: Bearer` header to RunPod (not in URLs)
- **API authentication is not implemented yet.** Protect the orchestrator behind a reverse proxy, VPN, or add API key middleware before public exposure

## Architecture

See [docs/Arquitetura e Design do Sistema.md](docs/Arquitetura%20e%20Design%20do%20Sistema.md) for system design and [docs/Especificação Funcional e Requisitos - RunPod Orchestrator.md](docs/Especificação%20Funcional%20e%20Requisitos%20-%20RunPod%20Orchestrator.md) for requirements.
