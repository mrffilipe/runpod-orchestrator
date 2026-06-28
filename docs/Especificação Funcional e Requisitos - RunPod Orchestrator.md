# Especificação Funcional e Requisitos - RunPod Orchestrator

## 1. Visão Geral
O **RunPod Orchestrator** é uma camada de middleware projetada para simular um comportamento **serverless** utilizando a infraestrutura de **Pods** do RunPod. O objetivo principal é reduzir custos operacionais ligando a GPU apenas quando houver demanda e desligando-a após um período de inatividade, evitando as falhas de provisionamento comuns no serviço serverless nativo.

## 2. Objetivos
- **Redução de Custos**: Pagar pela GPU apenas durante o uso efetivo.
- **Confiabilidade**: Garantir que o ambiente (vLLM) esteja sempre pronto com dependências e modelos pré-carregados em volumes persistentes.
- **Transparência**: Oferecer uma API compatível com OpenAI para os clientes finais.

## 3. Requisitos Funcionais (RF)

### RF01 - Gerenciamento de Ciclo de Vida do Pod
O sistema deve ser capaz de:
- **Descobrir automaticamente** o pod gerenciado: listar pods da conta (`myself.pods`), reutilizar um pod compatível (GPU na lista de preferência) ou **criar um novo** via `podFindAndDeployOnDemand`.
- Verificar o status atual do Pod (RUNNING, STOPPED, etc.).
- Iniciar um Pod parado (`podResume`).
- Parar um Pod em execução (`podStop`) após tempo de inatividade.
- Selecionar GPU por **lista de prioridade** configurável (`RUNPOD_PREFERRED_GPU_TYPES`), escolhendo a primeira com disponibilidade (`stockStatus` ≠ None).

O ID do pod é **transparente** para clientes da API — apenas o orquestrador interage com a API RunPod.

### RF02 - Proxy de Requisições de Inferência
O sistema deve:
- Receber requisições compatíveis com a API da OpenAI (`/v1/chat/completions`).
- Reter a requisição enquanto o Pod está iniciando (Cold Start).
- Encaminhar a requisição para o endpoint interno do Pod assim que ele estiver saudável.

### RF03 - Health Check e Readiness
O sistema deve validar se o serviço vLLM dentro do Pod está pronto para receber tráfego antes de encaminhar requisições.

### RF04 - Gerenciamento de Inatividade
O sistema deve monitorar o tempo desde a última requisição e emitir um comando de `Stop` após X minutos (padrão: 15 min) de inatividade.

### RF05 - Controle de Concorrência
O sistema deve garantir que múltiplas requisições simultâneas durante o Cold Start não disparem múltiplos comandos de `Start` para o mesmo Pod.

## 4. Requisitos Não Funcionais (RNF)
- **RNF01 - Latência**: O overhead da camada de orquestração deve ser mínimo (excluindo o tempo de boot do Pod).
- **RNF02 - Resiliência**: Implementar estratégias de retry para chamadas à API do RunPod.
- **RNF03 - Segurança**: Armazenamento seguro da API Key do RunPod.
- **RNF04 - Escalabilidade**: Estrutura preparada para gerenciar múltiplos Pods ou tipos de GPU no futuro.

## 5. Casos de Uso Principais
1. **Primeira requisição (sem pod compatível)**: Cliente chama API → Orquestrador lista pods → Nenhum compatível → Seleciona GPU disponível → Cria pod → Aguarda Readiness → Encaminha chamada.
2. **Requisição com Pod Parado**: Cliente chama API → Orquestrador reutiliza pod parado compatível → Resume → Aguarda Readiness → Encaminha Chamada → Retorna Resposta.
3. **Requisição com Pod Ativo**: Cliente chama API → Orquestrador detecta RUNNING → Encaminha Chamada → Retorna Resposta.
4. **Timeout de Inatividade**: 15 minutos sem chamadas → Orquestrador envia comando de Stop.
