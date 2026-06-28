# Integração Open WebUI

Artefatos opcionais para complementar o stack **RunPod Orchestrator + Open WebUI + vLLM**.

## Advanced Statistics Filter

Filtro que exibe estatísticas detalhadas ao final de cada resposta do assistente: tokens (prompt, resposta, total), TTFT, tempo de raciocínio, tempo total e tokens/s.

Arquivo: [`filters/advanced_statistics_filter.py`](filters/advanced_statistics_filter.py)

### Pré-requisitos

1. Open WebUI apontando para o orquestrador (`OPENAI_API_BASE_URL=http://orchestrator:8080/v1` no `docker-compose.yml`).
2. Capability **Usage** ativada no modelo (Workspace → Models → editar modelo → Capabilities → **Usage**). Isso faz o WebUI enviar `stream_options: { "include_usage": true }` e o vLLM devolver contagem de tokens.
3. **Streaming** recomendado nas conversas para métricas de tempo mais precisas.

### Importar o filtro

1. Abra o Open WebUI em [http://localhost:3000](http://localhost:3000).
2. Vá em **Admin Panel** → **Functions** (ou **Workspace** → **Functions**).
3. Clique em **Import** (ou **+** → Import from file).
4. Cole o conteúdo de [`filters/advanced_statistics_filter.py`](filters/advanced_statistics_filter.py) ou faça upload do arquivo.
5. Ative o filtro após importar.

O filtro fica persistido no volume Docker `open-webui-data`; não é necessário remontar o compose.

### Vincular ao modelo

1. **Workspace** → **Models** → edite o modelo usado (ex.: `Qwen/Qwen3-8B`).
2. Em **Capabilities**, marque **Usage** (se ainda não estiver).
3. Em **Filters**, selecione **Advanced Statistics Filter**.
4. Salve o modelo.

### O que vem do vLLM vs. o que o filtro calcula

| Métrica | Origem |
| :--- | :--- |
| `prompt_tokens`, `completion_tokens`, `total_tokens` | vLLM via capability **Usage** |
| TTFT, tempo de raciocínio, tokens/s de geração | Calculados pelo filtro (timestamps da UI e blocos de raciocínio) |

O vLLM **não** expõe `prompt_eval_duration` / `eval_duration` no formato Ollama. Nesses casos o filtro usa a **Estratégia B**: timestamps de raciocínio (modelos Qwen/DeepSeek) e tempo total medido entre `inlet` e `outlet`.

### Limitações

- Se `usage` estiver vazio ou `completion_tokens == 0`, a tabela **não** é anexada (comportamento intencional do filtro).
- TTFT e tokens/s de geração podem aparecer como `N/A` quando não há timestamps de raciocínio nem métricas nativas do backend.
- O filtro opera no Open WebUI; o orquestrador repassa o body da requisição intacto para o vLLM — nenhuma alteração no backend .NET é necessária.

### Desativar temporariamente

No Open WebUI, edite o filtro importado e desmarque a válvula **enable_stats**, ou remova o filtro das Capabilities do modelo.
