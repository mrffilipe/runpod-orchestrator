# RunPod Orchestrator

Middleware ASP.NET Core que simula **inferência GPU serverless** em Pods do RunPod. Descobre ou cria automaticamente um pod gerenciado, retoma sob demanda, faz proxy de requisições compatíveis com OpenAI para o vLLM e para o pod após timeout de inatividade.

**Início rápido com Docker:** veja [GETTING_STARTED.md](GETTING_STARTED.md)

Documentação em inglês: [README.md](README.md)

## Como funciona

Cada instância do orquestrador gerencia **um pod** de forma transparente:

1. Lista os pods existentes na sua conta RunPod
2. Reutiliza pod em execução ou parado cujo **nome** coincide com `RunPod__PodName` (prefere GPUs da lista de prioridade)
3. Se nenhum existir, encerra pods remanescentes com esse nome e implanta um novo na primeira GPU disponível
4. Readiness em camadas do vLLM (`/health` + `/v1/models`), revalidação em runtime, retry do proxy em falha upstream, auto-stop por ociosidade e terminate opcional após idle prolongado

Health checks e proxy de inferência usam o proxy HTTP da RunPod: `https://{podId}-{porta}.proxy.runpod.net` (porta padrão `8000` via `VllmPort`).

Clientes da API nunca informam um ID de pod.

## Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download) para desenvolvimento local
- [Docker](https://www.docker.com/) para deploy containerizado
- Conta RunPod com chave de API

## Imagem Docker (recomendado)

Baixe a imagem publicada:

```bash
docker pull mrffilipe/runpod-orchestrator:latest
```

Veja [GETTING_STARTED.md](GETTING_STARTED.md) para exemplos completos de `docker-compose.yml` e `.env`.

O `docker-compose.yml` na raiz também inclui **Open WebUI** na porta `3000` para testar o chat rapidamente contra o orquestrador.

### Build local

```bash
docker build -f backend/Dockerfile -t runpod-orchestrator:latest .
```

## Desenvolvimento local

```powershell
cd backend/RunpodOrchestrator.API

$env:RunPod__ApiKey = "sua-chave-runpod"
$env:RunPod__PreferredGpuTypeIds__0 = "NVIDIA GeForce RTX 4090"

dotnet run
```

Swagger UI disponível em Development em `/swagger`. Health check: `GET /health`

## Variáveis de ambiente

Configuração via convenção ASP.NET Core: `Section__Property` (double underscore). Padrões em `appsettings.json`; sobrescreva via ambiente ou `.env` no Docker Compose.

| Variável | Obrigatória | Padrão | Descrição |
|----------|-------------|--------|-----------|
| `RunPod__ApiKey` | Sim | — | Chave API RunPod. Use gerenciador de segredos em produção. Nunca faça commit. |
| `RunPod__PreferredGpuTypeIds__0`, `__1`, … | Não | `NVIDIA GeForce RTX 4090` | GPUs em ordem de prioridade (lista indexada) |
| `RunPod__PodName` | Não | `runpod-orchestrator` | Nome para pods recém-criados |
| `RunPod__PodTemplateId` | Não | — | ID do template RunPod (modo template-first) |
| `RunPod__PodImageName` | Não | `vllm/vllm-openai:latest` | Imagem Docker (ignorada se `RunPod__PodTemplateId` estiver definido) |
| `RunPod__PodPorts` | Não | `8000/http` | Portas expostas para novos pods |
| `RunPod__VolumeMountPath` | Não | `/workspace` | Caminho de montagem do volume no pod |
| `RunPod__VolumeInGb` | Não | `40` | Tamanho do volume (GB) |
| `RunPod__ContainerDiskInGb` | Não | `40` | Tamanho do disco do container (GB) |
| `RunPod__NetworkVolumeId` | Não | — | Network volume persistente opcional |
| `Logging__LogLevel__Default` | Não | `Information` | Nível de log da aplicação |
| `RunPod__RequestTimeoutSeconds` | Não | `30` | Timeout do client GraphQL RunPod |
| `RunPod__VllmHealthCheckPath` | Não | `/health` | Path do health check vLLM |
| `RunPod__VllmHealthCheckRetries` | Não | `12` | Tentativas máximas de health check vLLM |
| `RunPod__VllmHealthCheckRetryDelaySeconds` | Não | `5` | Delay entre retries de health |
| `RunPod__VllmHealthCheckTimeoutSeconds` | Não | `10` | Timeout do client HTTP de health vLLM |
| `RunPod__VllmProxyTimeoutSeconds` | Não | `600` | Timeout do proxy de inferência |
| `RunPod__VllmDefaultModel` | Não | `Qwen/Qwen3-8B` | ID do modelo para `GET /v1/models` e `MODEL_NAME` no deploy |
| `RunPod__PodEnvironment__*` | Não | — | Env vars extras no deploy (ex.: `RunPod__PodEnvironment__HF_TOKEN=...`) |
| `RunPod__PodPollingIntervalSeconds` | Não | `5` | Intervalo de polling do runtime |
| `RunPod__PodStartupTimeoutSeconds` | Não | `600` | Tempo máximo de espera no startup |
| `RunPod__PodResumeMaxRetries` | Não | `3` | Retries de resume com backoff exponencial |
| `RunPod__PodResumeRetryBaseDelaySeconds` | Não | `2` | Delay base do backoff de resume |
| `RunPod__PodHealthRecoveryEnabled` | Não | `true` | Stop+resume após falha de health vLLM |
| `RunPod__VllmReadinessModelsPath` | Não | `/v1/models` | Path para checagem de modelo no startup |
| `RunPod__VllmReadinessRequireModel` | Não | `true` | Exige modelo primário na resposta de `/v1/models` |
| `RunPod__VllmRuntimeHealthTtlSeconds` | Não | `300` | Revalida health após N segundos em Ready |
| `RunPod__VllmRuntimeHealthRetries` | Não | `3` | Retries rápidos na revalidação de runtime |
| `RunPod__VllmRuntimeHealthRetryDelaySeconds` | Não | `2` | Delay entre retries de health em runtime |
| `RunPod__VllmProxyRetryOnFailure` | Não | `true` | Retry único após falha 502/503/504 |
| `RunPod__PodIdleTimeoutMinutes` | Não | `15` | Auto-stop após minutos ociosos |
| `RunPod__PodIdleCheckIntervalSeconds` | Não | `60` | Intervalo do monitor de inatividade |
| `RunPod__PodTerminateCheckIntervalMinutes` | Não | `60` | Intervalo da checagem de terminate pós-idle |
| `RunPod__PodTerminateAfterIdleEnabled` | Não | `true` | Termina pods `RunPod__PodName` após idle persistente |

## Endpoints da API

| Método | Path | Descrição |
|--------|------|-----------|
| `GET` | `/v1/models` | Lista de modelos configurados para clientes OpenAI (estática; sem acordar GPU) |
| `POST` | `/v1/chat/completions` | Proxy para vLLM (compatível OpenAI) |
| `POST` | `/v1/runpod/ensure-ready` | Garante que o pod gerenciado está pronto |
| `GET` | `/v1/runpod/status` | Status do pod gerenciado via API RunPod |
| `GET` | `/v1/runpod/idle-status` | Estado do monitor de inatividade |
| `POST` | `/v1/runpod/terminate-managed` | Termina todos os pods com nome `RunPod__PodName` |
| `GET` | `/health` | Health probe do orquestrador |

Endpoints de diagnóstico em `/v1/runpod/*` são para operações e debug.

## CI/CD — publicação no Docker Hub

O workflow GitHub Actions [`.github/workflows/docker-publish.yml`](.github/workflows/docker-publish.yml) compila e publica `mrffilipe/runpod-orchestrator`.

**Secret obrigatório no repositório:**

| Secret | Descrição |
|--------|-----------|
| `DOCKERHUB_TOKEN` | Token de acesso Docker Hub do usuário `mrffilipe` (permissão de push) |

**Gatilhos:**

- Manual: Actions → *Docker publish* → *Run workflow*
- Push de tag: `docker-v1.0.0` (tags semver aplicadas à imagem)

## Notas de produção

1. Armazene `RunPod__ApiKey` em um gerenciador de segredos (Azure Key Vault, AWS Secrets Manager, etc.)
2. Execute em VPS de baixo custo, pod CPU RunPod ou Kubernetes
3. Aponte clientes para `POST /v1/chat/completions`
4. Ajuste `RunPod__PodIdleTimeoutMinutes` para controlar custo de GPU
5. Use `GET /health` para probes de load balancer e container
6. Defina `ASPNETCORE_ENVIRONMENT=Production` (Swagger desabilitado)

## Segurança

- A chave RunPod é lida apenas via ambiente — nunca hardcode
- A chave é enviada via header `Authorization: Bearer` ao RunPod (não em URLs)
- **Autenticação da API ainda não implementada.** Proteja o orquestrador atrás de reverse proxy, VPN ou adicione middleware de API key antes de exposição pública

## Arquitetura

Veja [docs/Arquitetura e Design do Sistema.md](docs/Arquitetura%20e%20Design%20do%20Sistema.md) para o design do sistema e [docs/Especificação Funcional e Requisitos - RunPod Orchestrator.md](docs/Especificação%20Funcional%20e%20Requisitos%20-%20RunPod%20Orchestrator.md) para requisitos.
