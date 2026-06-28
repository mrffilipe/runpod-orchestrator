# Referências Técnicas da API RunPod

Este documento contém os snippets de GraphQL necessários para a implementação, baseados na documentação oficial.

## 1. Endpoint GraphQL e Autenticação

**Endpoint:** `https://api.runpod.io/graphql`

**Autenticação:** Header HTTP (implementado no orquestrador):

```http
Authorization: Bearer {YOUR_API_KEY}
Content-Type: application/json
```

> A API key **nunca** deve aparecer na URL ou em logs.

## 2. Listar Pods da Conta

**Query:**
```graphql
query {
  myself {
    pods {
      id
      name
      desiredStatus
      runtime {
        uptimeInSeconds
      }
      machine {
        gpuDisplayName
      }
    }
  }
}
```

Usado pelo `ManagedPodResolver` para reutilizar pods compatíveis com a lista `PreferredGpuTypeIds`.

## 3. Consultar Status do Pod
**Query:**
```graphql
query {
  pod(input: {podId: "YOUR_POD_ID"}) {
    id
    name
    runtime {
      uptimeInSeconds
      ports {
        ip
        isIpPublic
        privatePort
        publicPort
      }
    }
  }
}
```
*Nota: O campo `runtime` retornará nulo se o pod estiver parado.*

## 4. Criar e Implantar Pod (Deploy)

O orquestrador suporta dois modos de deploy:

### 4a. Deploy via template (recomendado para templates personalizados)

Quando `RUNPOD_POD_TEMPLATE_ID` está definido, o orquestrador envia `templateId` na mutation e **não** reenvia `imageName`, `ports`, `volumeMountPath` ou tamanhos de disco — o template define esses valores. Ainda são enviados: GPU, nome, `networkVolumeId` (se configurado) e variáveis de ambiente (`MODEL_NAME` + `RUNPOD_POD_ENV`).

Templates **privados** (não publicados na comunidade) funcionam normalmente desde que pertençam à **mesma conta** da `RUNPOD_API_KEY`. Se o template usa imagem Docker privada, configure `containerRegistryAuthId` no próprio template no console RunPod.

**Mutation (modo template):**
```graphql
mutation {
  podFindAndDeployOnDemand(input: {
    cloudType: ALL
    gpuCount: 1
    gpuTypeId: "NVIDIA GeForce RTX 4090"
    name: "runpod-orchestrator"
    templateId: "fqw714oyyl"
    networkVolumeId: "optional-volume-id"
    env: [
      { key: "MODEL_NAME", value: "Qwen/Qwen3-8B" }
    ]
  }) {
    id
    desiredStatus
    machine {
      gpuDisplayName
    }
  }
}
```

### 4b. Deploy via imagem Docker

Quando `RUNPOD_POD_TEMPLATE_ID` **não** está definido, o deploy usa `imageName`, portas e volumes explicitamente.

**Mutation:**
```graphql
mutation {
  podFindAndDeployOnDemand(input: {
    cloudType: ALL
    gpuCount: 1
    volumeInGb: 40
    containerDiskInGb: 40
    gpuTypeId: "NVIDIA GeForce RTX 4090"
    name: "runpod-orchestrator"
    imageName: "vllm/vllm-openai:latest"
    ports: "8000/http"
    volumeMountPath: "/workspace"
    networkVolumeId: "optional-volume-id"
    env: [
      { key: "MODEL_NAME", value: "Qwen/Qwen3-8B" }
      { key: "ENABLE_AUTO_TOOL_CHOICE", value: "true" }
      { key: "TOOL_CALL_PARSER", value: "hermes" }
    ]
  }) {
    id
    desiredStatus
    machine {
      gpuDisplayName
    }
  }
}
```

Chamado quando nenhum pod compatível existe na conta. O orquestrador monta `env` a partir de `VLLM_DEFAULT_MODEL` (`MODEL_NAME`) e `RUNPOD_POD_ENV` (pares `KEY=VALUE` separados por vírgula). Com `RUNPOD_POD_TEMPLATE_ID`, usa o modo template (4a); caso contrário, o modo imagem (4b).

## 5. Retomar Pod (Resume)
**Mutation:**
```graphql
mutation {
  podResume(input: {
    podId: "YOUR_POD_ID",
    gpuCount: 1
  }) {
    id
    desiredStatus
  }
}
```

## 6. Parar Pod (Stop)
**Mutation:**
```graphql
mutation {
  podStop(input: {podId: "YOUR_POD_ID"}) {
    id
    desiredStatus
  }
}
```

## 7. Terminar Pod (Terminate)
Remove permanentemente o pod e o disco do container (dados fora de network volume são perdidos).

**Mutation:**
```graphql
mutation {
  podTerminate(input: {podId: "YOUR_POD_ID"})
}
```

## 8. Verificar Disponibilidade de GPU
**Query:**
```graphql
query {
  gpuTypes(input: { id: "NVIDIA GeForce RTX 4090" }) {
    id
    displayName
    lowestPrice(input: { gpuCount: 1, secureCloud: true }) {
      stockStatus
      availableGpuCounts
    }
  }
}
```
*Valores de `stockStatus`: High, Medium, Low, None. O orquestrador aceita High/Medium/Low.*

## 8. Endpoints do vLLM (Dentro do Pod)

O orquestrador conecta ao vLLM via **proxy RunPod** (não usa `ip:publicPort` da API GraphQL):

- **Base URL**: `https://{podId}-{portaInterna}.proxy.runpod.net` (ex.: `https://abc123-8000.proxy.runpod.net`)
- **Health Check**: `GET .../health`
- **Listar Modelos**: `GET .../v1/models`
- **Chat Completions**: `POST .../v1/chat/completions`

A `{portaInterna}` corresponde a `VllmPort` / porta exposta no template (padrão `8000`).

O orquestrador expõe `GET /v1/models` **estático** (de `VLLM_DEFAULT_MODEL`) para clientes OpenAI como o Open WebUI, sem acordar o pod GPU.

## 9. Configurações Recomendadas
- **Template RunPod**: Defina `RUNPOD_POD_TEMPLATE_ID` para usar um template da sua conta (público ou privado). Imagem, portas e disco vêm do template.
- **Imagem Docker**: `vllm/vllm-openai:latest` (ou imagem personalizada via `RUNPOD_POD_IMAGE`, quando template ID não está definido).
- **Volume**: Montar em `/workspace` ou onde o cache do HuggingFace estiver configurado (`HF_HOME`).
- **Portas**: Expor a porta 8000 (padrão vLLM) como HTTP (`8000/http`).
- **GPUs preferidas**: Lista CSV em `RUNPOD_PREFERRED_GPU_TYPES`, ordem = prioridade.
