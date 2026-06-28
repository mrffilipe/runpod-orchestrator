# Arquitetura e Design do Sistema

## 1. Diagrama de Blocos
```text
[ Cliente ] 
     │
     ▼
[ API Gateway / Orchestrator (ASP.NET Core) ]
     │
     ├── [ Managed Pod Resolver ] ──► [ RunPod GraphQL API (list / deploy) ]
     │
     ├── [ Pod Manager Service ] ───► [ RunPod GraphQL API (resume / status) ]
     │
     ├── [ Proxy Service ] ─────────► [ Pod vLLM Endpoint ]
     │
     └── [ Idle Monitor (Background Service) ]
```

## 2. Máquina de Estados do Pod
O orquestrador mapeia os estados internos do RunPod para uma máquina de estados simplificada:

- **STOPPED**: Pod parado ou orquestrador em repouso.
- **STARTING**: Transição disparada por uma requisição (resume ou cold start).
- **READY**: Pod em RUNNING e Health Check do vLLM aprovado.
- **FAULTED**: Pod em estado irrecuperável (falha persistente de health check); requer intervenção manual.

Estados futuros opcionais: **BUSY** (processando), **IDLE** (timer de inatividade ativo).

## 3. Resolução do Pod Gerenciado
Antes de qualquer operação de ciclo de vida, o `ManagedPodResolver`:

1. Lista pods via `myself { pods { ... } }`
2. Filtra pods com **nome** igual a `PodName` (`RunPod__PodName`)
3. Prefere pod **running** gerenciado; senão pod **parado** gerenciado (prioriza GPU preferida)
4. Se nenhum existir: `podTerminate` em pods remanescentes com esse nome, consulta disponibilidade de GPU e chama `podFindAndDeployOnDemand`
5. Cacheia o `podId` resolvido (invalida em 404)

## 4. Fluxo de Sequência (Cold Start)
1. **Request Inbound**: API recebe POST `/v1/chat/completions`.
2. **Resolve Pod**: `ManagedPodResolver` descobre ou cria o pod gerenciado.
3. **State Check**: Consulta `pod(input: { podId })` via GraphQL.
4. **Trigger Start**: Se `runtime` for nulo, chama `podResume`.
5. **Polling Readiness**: 
   - Loop verifica ports no runtime.
   - Após runtime disponível, tenta `GET /health` no IP do Pod.
6. **Forwarding**: Envia payload original para o Pod.
7. **Response**: Retorna stream/json para o cliente.
8. **Reset Timer**: Atualiza timestamp de `LastActivity`.

## 5. Estratégia de Persistência
- **Network Volume** (opcional via `RunPod__NetworkVolumeId`): Recomendado para cache HuggingFace e pesos de modelo.
- **Vantagem**: O `podStop` mantém o volume montado. Ao fazer `podResume`, o container sobe em segundos e o vLLM carrega do disco para a VRAM.

## 6. Design de Código (Implementado)

### IRunPodApiClient
Encapsula chamadas GraphQL: list pods, deploy, resume, stop, status, GPU availability.

### IManagedPodResolver
Resolve ou cria o pod gerenciado (singleton, thread-safe).

### IPodManagerService
Garante que o pod está pronto. Usa `SemaphoreSlim` + `TaskCompletionSource` para request coalescing.

### IdleMonitorService (BackgroundService)
Verifica inatividade a cada `PodIdleCheckIntervalSeconds` e envia `podStop`. A cada `PodTerminateCheckIntervalMinutes`, se o idle persistir além de `PodIdleTimeoutMinutes`, envia `podTerminate` para todos os pods com `PodName` (economia de disco do container).

## 7. Prevenção de Race Conditions
Padrão de **Request Coalescing**:
- Se uma requisição disparar o `Start`, ela cria uma `TaskCompletionSource`.
- Requisições subsequentes aguardam a mesma Task enquanto o estado é `STARTING`.

## 8. Deploy
- Imagem Docker publicada via GitHub Actions (`.github/workflows/docker-publish.yml`)
- Health check do orquestrador: `GET /health`
- Configuração via `appsettings.json` e variáveis de ambiente ASP.NET Core (`RunPod__ApiKey`, `RunPod__PreferredGpuTypeIds__0`, etc.)
