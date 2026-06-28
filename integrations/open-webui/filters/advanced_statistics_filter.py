"""
title: Advanced Statistics Filter
author: Open WebUI Community / Adaptação
description: Estatísticas detalhadas com TTFT e Tempo de Raciocínio (Design Profissional e Limpo).
version: 1.3.0
"""

import time
from pydantic import BaseModel, Field
from typing import Optional


class Filter:
    class Valves(BaseModel):
        enable_stats: bool = Field(
            default=True,
            description="Ativar a exibição da tabela de estatísticas ao final de cada resposta.",
        )

    def __init__(self):
        self.valves = self.Valves()
        # Dicionário na memória da classe para persistir o tempo inicial entre inlet e outlet
        self.request_times = {}

    def inlet(self, body: dict, __user__: Optional[dict] = None) -> dict:
        # Usa o message_id do metadata para rastrear a requisição com precisão
        req_id = body.get("metadata", {}).get("message_id")
        if req_id:
            self.request_times[req_id] = time.time()
        return body

    def outlet(self, body: dict, __user__: Optional[dict] = None) -> dict:
        if not self.valves.enable_stats:
            return body

        messages = body.get("messages", [])
        if not messages:
            return body

        assistant = messages[-1]

        if assistant.get("role") != "assistant":
            return body

        # Tenta recuperar o tempo de início exato
        req_id = body.get("id")
        start_time = self.request_times.pop(req_id, None)
        end_time = time.time()

        total_time = (end_time - start_time) if start_time else 0.0

        content = assistant.get("content", "")
        model_name = body.get("model", "Desconhecido")

        # 1. Extração de Uso (Tokens)
        usage = assistant.get("usage", {})
        info = assistant.get("info", {}) or {}

        if not usage and isinstance(info, dict):
            usage = info.get("usage", {})

        prompt_tokens = usage.get("prompt_tokens", 0)
        completion_tokens = usage.get("completion_tokens", 0)
        total_tokens = usage.get("total_tokens", 0)

        if not usage or completion_tokens == 0:
            return body

        # 2. Cálculo de Tempos (TTFT, Raciocínio, Geração)
        ttft = -1.0
        reasoning_time = 0.0
        gen_time = -1.0

        # Busca timestamps nativos no bloco de raciocínio (Ex: Qwen/DeepSeek)
        r_start = None
        r_end = None
        for item in assistant.get("output", []):
            if item.get("type") == "reasoning":
                r_start = item.get("started_at")
                r_end = item.get("ended_at")
                break

        # Estratégia A: Métricas nativas do backend (Ollama/vLLM)
        prompt_eval_ns = info.get("prompt_eval_duration", 0)
        eval_ns = info.get("eval_duration", 0)

        if prompt_eval_ns > 0 and eval_ns > 0:
            ttft = prompt_eval_ns / 1e9
            gen_time = eval_ns / 1e9
            if r_start and r_end:
                reasoning_time = r_end - r_start

        # Estratégia B: Cálculo por Timestamps da interface (Para APIs externas compatíveis)
        elif r_start and r_end:
            reasoning_time = r_end - r_start
            if start_time:
                # O tempo até o início do raciocínio é o pre-fill / latência
                ttft = r_start - start_time
            # O tempo de geração de texto é o tempo total menos quando o raciocínio acabou
            gen_time = end_time - r_end

        # 3. Velocidades
        avg_tps = (completion_tokens / total_time) if total_time > 0 else 0.0
        gen_tps = (completion_tokens / gen_time) if gen_time > 0 else 0.0

        # Formatação dos Textos para evitar quebras visuais
        str_total = f"{total_time:.2f}s" if total_time > 0 else "N/A"
        str_ttft = f"{ttft:.2f}s" if ttft >= 0 else "N/A"
        str_reasoning = f"{reasoning_time:.2f}s" if reasoning_time > 0 else "-"
        str_gen_tps = f"{gen_tps:.2f} t/s" if gen_tps > 0 else "N/A"
        str_avg_tps = f"{avg_tps:.2f} t/s" if avg_tps > 0 else "N/A"

        # 4. Construção do Layout (Tabela de 3 Colunas Profissional)
        stats_html = f"""

<details>
<summary><b>Estatísticas:</b> <code>{model_name}</code></summary>

| Tokens | Tempos | Velocidade |
| :--- | :--- | :--- |
| **Prompt:** `{prompt_tokens}` | **TTFT:** `{str_ttft}` | **Geração:** `{str_gen_tps}` |
| **Resposta:** `{completion_tokens}` | **Raciocínio:** `{str_reasoning}` | **Média:** `{str_avg_tps}` |
| **Total:** `{total_tokens}` | **Total:** `{str_total}` | - |

</details>
"""

        assistant["content"] = content + stats_html
        return body
