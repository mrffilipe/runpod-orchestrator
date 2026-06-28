# Getting Started

## English

This guide shows how to run the **RunPod Orchestrator** using the published Docker image. The orchestrator automatically discovers, creates, and manages a single RunPod GPU pod — no pod ID is required from API clients.

### Prerequisites

- [Docker](https://www.docker.com/) and Docker Compose
- A [RunPod](https://www.runpod.io/) account with an API key
- (Optional) A RunPod network volume ID for persistent model cache

### 1. Pull the image

Published as `mrffilipe/runpod-orchestrator`:

```bash
docker pull mrffilipe/runpod-orchestrator:latest
```

### 2. Create a `.env` file

Create a `.env` file next to your `docker-compose.yml` (do not commit it):

```env
# Required
RunPod__ApiKey=your-runpod-api-key

# GPU & pod creation (used when deploying a new pod)
RunPod__PreferredGpuTypeIds__0=NVIDIA GeForce RTX 4090
RunPod__PreferredGpuTypeIds__1=NVIDIA RTX A6000
RunPod__PodName=runpod-orchestrator
# RunPod__PodTemplateId=fqw714oyyl
RunPod__PodImageName=vllm/vllm-openai:latest
RunPod__PodPorts=8000/http
RunPod__VolumeMountPath=/workspace
RunPod__VolumeInGb=40
RunPod__ContainerDiskInGb=40
RunPod__NetworkVolumeId=

# Cost control
RunPod__PodIdleTimeoutMinutes=15
RunPod__PodIdleCheckIntervalSeconds=60
RunPod__PodTerminateCheckIntervalMinutes=60
RunPod__PodTerminateAfterIdleEnabled=true

# Startup & resilience
RunPod__PodStartupTimeoutSeconds=600
RunPod__PodPollingIntervalSeconds=5
RunPod__PodResumeMaxRetries=3
RunPod__PodResumeRetryBaseDelaySeconds=2
RunPod__PodHealthRecoveryEnabled=true

# vLLM proxy & readiness
RunPod__VllmProxyTimeoutSeconds=600
RunPod__VllmProxyRetryOnFailure=true
RunPod__VllmHealthCheckPath=/health
RunPod__VllmHealthCheckRetries=12
RunPod__VllmHealthCheckRetryDelaySeconds=5
RunPod__VllmHealthCheckTimeoutSeconds=10
RunPod__VllmReadinessModelsPath=/v1/models
RunPod__VllmReadinessRequireModel=true
RunPod__VllmRuntimeHealthTtlSeconds=300
RunPod__VllmRuntimeHealthRetries=3
RunPod__VllmRuntimeHealthRetryDelaySeconds=2

# vLLM model (Open WebUI discovery + pod deploy)
RunPod__VllmDefaultModel=Qwen/Qwen3-8B
RunPod__PodEnvironment__ENABLE_AUTO_TOOL_CHOICE=true
RunPod__PodEnvironment__TOOL_CALL_PARSER=hermes

# Logging
Logging__LogLevel__Default=Information
```

Full reference: [README.md — Environment variables](README.md#environment-variables)

### 3. Create a `docker-compose.yml`

```yaml
services:
  orchestrator:
    image: mrffilipe/runpod-orchestrator:latest
    container_name: runpod-orchestrator
    ports:
      - "8080:8080"
    env_file:
      - .env
    environment:
      ASPNETCORE_ENVIRONMENT: Production
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 5s
      retries: 3
      start_period: 15s
    restart: unless-stopped

  open-webui:
    image: openwebui/open-webui:latest
    container_name: open-webui
    ports:
      - "3000:8080"
    environment:
      OPENAI_API_BASE_URL: http://orchestrator:8080/v1
      OPENAI_API_KEY: dummy
    depends_on:
      orchestrator:
        condition: service_healthy
    volumes:
      - open-webui-data:/app/backend/data
    restart: unless-stopped

volumes:
  open-webui-data:
```

### 4. Start the stack

Start the orchestrator and Open WebUI:

```bash
docker compose up -d
curl http://localhost:8080/health
```

### 5. Test with Open WebUI (recommended)

1. Open [http://localhost:3000](http://localhost:3000)
2. On first visit, create a local admin account (required by Open WebUI)
3. Ensure the Open WebUI connection points to `http://orchestrator:8080/v1` with API key `dummy`
4. The model **`Qwen/Qwen3-8B`** (from `RunPod__VllmDefaultModel`) should appear in the model selector via `GET /v1/models` — no GPU cold start required for listing
5. Select that model and send a message — the orchestrator resolves or creates the managed pod automatically (no pod ID needed)

The first message may take several minutes while the GPU pod starts (cold start). The orchestrator does not require API authentication yet — any API key value (e.g. `dummy`, as set in the compose file) is fine.

**Note:** If the orchestrator **reuses an existing RunPod pod**, that pod keeps its original `MODEL_NAME` from the RunPod console. Align `RunPod__VllmDefaultModel` with the pod's model or create a new pod.

#### Detailed statistics (optional)

For detailed metrics (TTFT, tokens/s, reasoning time) that vLLM does not expose natively in Open WebUI, import the **Advanced Statistics Filter** and enable the **Usage** capability on your model. See [integrations/open-webui/README.md](integrations/open-webui/README.md).

### 6. Send an inference request (curl / API)

Alternatively, call the orchestrator directly:

```bash
curl -X POST http://localhost:8080/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{
    "model": "Qwen/Qwen3-8B",
    "messages": [{"role": "user", "content": "Hello!"}]
  }'
```

The first request may take several minutes while the pod starts (cold start).

### Next steps

- Full configuration reference: [README.md](README.md)
- Architecture and requirements: [docs/](docs/)

---

## Português

Este guia mostra como executar o **RunPod Orchestrator** usando a imagem Docker publicada. O orquestrador descobre, cria e gerencia automaticamente um único pod GPU no RunPod — nenhum ID de pod é necessário para os clientes da API.

### Pré-requisitos

- [Docker](https://www.docker.com/) e Docker Compose
- Conta no [RunPod](https://www.runpod.io/) com chave de API
- (Opcional) ID de network volume do RunPod para cache persistente de modelos

### 1. Baixar a imagem

Publicada como `mrffilipe/runpod-orchestrator`:

```bash
docker pull mrffilipe/runpod-orchestrator:latest
```

### 2. Criar um arquivo `.env`

Crie um arquivo `.env` ao lado do seu `docker-compose.yml` (não faça commit dele):

```env
# Obrigatório
RunPod__ApiKey=sua-chave-runpod

# GPU e criação do pod (usado ao implantar um pod novo)
RunPod__PreferredGpuTypeIds__0=NVIDIA GeForce RTX 4090
RunPod__PreferredGpuTypeIds__1=NVIDIA RTX A6000
RunPod__PodName=runpod-orchestrator
# RunPod__PodTemplateId=
RunPod__PodImageName=vllm/vllm-openai:latest
RunPod__PodPorts=8000/http
RunPod__VolumeMountPath=/workspace
RunPod__VolumeInGb=40
RunPod__ContainerDiskInGb=40
RunPod__NetworkVolumeId=

# Controle de custo
RunPod__PodIdleTimeoutMinutes=15
RunPod__PodIdleCheckIntervalSeconds=60
RunPod__PodTerminateCheckIntervalMinutes=60
RunPod__PodTerminateAfterIdleEnabled=true

# Startup e resiliência
RunPod__PodStartupTimeoutSeconds=600
RunPod__PodPollingIntervalSeconds=5
RunPod__PodResumeMaxRetries=3
RunPod__PodResumeRetryBaseDelaySeconds=2
RunPod__PodHealthRecoveryEnabled=true

# Proxy vLLM e readiness
RunPod__VllmProxyTimeoutSeconds=600
RunPod__VllmProxyRetryOnFailure=true
RunPod__VllmHealthCheckPath=/health
RunPod__VllmHealthCheckRetries=12
RunPod__VllmHealthCheckRetryDelaySeconds=5
RunPod__VllmHealthCheckTimeoutSeconds=10
RunPod__VllmReadinessModelsPath=/v1/models
RunPod__VllmReadinessRequireModel=true
RunPod__VllmRuntimeHealthTtlSeconds=300
RunPod__VllmRuntimeHealthRetries=3
RunPod__VllmRuntimeHealthRetryDelaySeconds=2

# Modelo vLLM (descoberta no Open WebUI + deploy do pod)
RunPod__VllmDefaultModel=Qwen/Qwen3-8B
RunPod__PodEnvironment__ENABLE_AUTO_TOOL_CHOICE=true
RunPod__PodEnvironment__TOOL_CALL_PARSER=hermes

# Logging
Logging__LogLevel__Default=Information
```

Referência completa: [README.pt-BR.md — Variáveis de ambiente](README.pt-BR.md#variáveis-de-ambiente)

### 3. Criar um `docker-compose.yml`

```yaml
services:
  orchestrator:
    image: mrffilipe/runpod-orchestrator:latest
    container_name: runpod-orchestrator
    ports:
      - "8080:8080"
    env_file:
      - .env
    environment:
      ASPNETCORE_ENVIRONMENT: Production
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 5s
      retries: 3
      start_period: 15s
    restart: unless-stopped

  open-webui:
    image: openwebui/open-webui:latest
    container_name: open-webui
    ports:
      - "3000:8080"
    environment:
      OPENAI_API_BASE_URL: http://orchestrator:8080/v1
      OPENAI_API_KEY: dummy
    depends_on:
      orchestrator:
        condition: service_healthy
    volumes:
      - open-webui-data:/app/backend/data
    restart: unless-stopped

volumes:
  open-webui-data:
```

### 4. Iniciar o stack

Inicie o orquestrador e o Open WebUI:

```bash
docker compose up -d
curl http://localhost:8080/health
```

### 5. Testar com Open WebUI (recomendado)

1. Acesse [http://localhost:3000](http://localhost:3000)
2. Na primeira visita, crie uma conta admin local (exigência do Open WebUI)
3. Confirme que a conexão Open WebUI aponta para `http://orchestrator:8080/v1` com API key `dummy`
4. O modelo **`Qwen/Qwen3-8B`** (de `RunPod__VllmDefaultModel`) deve aparecer no seletor via `GET /v1/models` — sem cold start de GPU para listar
5. Selecione esse modelo e envie uma mensagem — o orquestrador resolve ou cria o pod gerenciado automaticamente (sem ID de pod)

A primeira mensagem pode levar vários minutos enquanto o pod GPU inicia (cold start). O orquestrador ainda não exige autenticação por API — qualquer valor de chave (ex.: `dummy`, como no compose) serve.

**Nota:** Se o orquestrador **reutilizar um pod existente** na RunPod, esse pod mantém o `MODEL_NAME` original do painel RunPod. Alinhe `RunPod__VllmDefaultModel` com o modelo do pod ou crie um pod novo.

#### Estatísticas detalhadas (opcional)

Para métricas detalhadas (TTFT, tokens/s, tempo de raciocínio) que o vLLM não expõe nativamente no Open WebUI, importe o **Advanced Statistics Filter** e ative a capability **Usage** no modelo. Veja [integrations/open-webui/README.md](integrations/open-webui/README.md).

### 6. Enviar uma requisição de inferência (curl / API)

Alternativamente, chame o orquestrador diretamente:

```bash
curl -X POST http://localhost:8080/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{
    "model": "Qwen/Qwen3-8B",
    "messages": [{"role": "user", "content": "Olá!"}]
  }'
```

A primeira requisição pode levar vários minutos enquanto o pod inicia (cold start).

### Próximos passos

- Referência completa de configuração: [README.pt-BR.md](README.pt-BR.md)
- Arquitetura e requisitos: [docs/](docs/)
