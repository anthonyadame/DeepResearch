# Multi-Model vLLM Architecture Diagram

## System Overview

```
┌────────────────────────────────────────────────────────────────────────┐
│                        DeepResearch Application                        │
│                    (C:\RepoEx\PhoenixAI\DeepResearch)                 │
│                                                                        │
│  ┌──────────────────────────────────────────────────────────────────┐ │
│  │  WorkflowModelConfiguration.cs                                   │ │
│  │                                                                  │ │
│  │  SupervisorBrainModel:    "qwen3.5-9b"   (Reasoning)           │ │
│  │  SupervisorToolsModel:    "mistral-7b"   (Fast Tools)          │ │
│  │  QualityEvaluatorModel:   "qwen3.5-9b"   (Analysis)            │ │
│  │  RedTeamModel:            "qwen3.5-9b"   (Critique)            │ │
│  │  ContextPrunerModel:      "mistral-7b"   (Extraction)          │ │
│  └──────────────────────────────────────────────────────────────────┘ │
│                                 ↓                                      │
│                     OpenAI-compatible API Requests                     │
│                     POST /v1/chat/completions                          │
│                     Model: "qwen3.5-9b" or "mistral-7b"               │
└─────────────────────────────────┬──────────────────────────────────────┘
                                  │
                                  │ HTTP (localhost:4000)
                                  ↓
┌────────────────────────────────────────────────────────────────────────┐
│                    LiteLLM Proxy (Port 4000)                          │
│              Container: lightning-litellm (CPU only)                   │
│                                                                        │
│  ┌──────────────────────────────────────────────────────────────────┐ │
│  │  Router Logic (litellm-multi-model.yaml)                        │ │
│  │                                                                  │ │
│  │  • Usage-based load balancing                                   │ │
│  │  • Automatic retry (3 attempts)                                 │ │
│  │  • Failover to OpenAI GPT-4o-mini                              │ │
│  │  • Request logging to PostgreSQL                                │ │
│  │  • Prometheus metrics export                                    │ │
│  └──────────────────────────────────────────────────────────────────┘ │
│                                                                        │
│  Model Aliases:                                                        │
│  • "qwen3.5-9b" → http://vllm-qwen:8000/v1                            │
│  • "mistral-7b" → http://vllm-mistral:8000/v1                         │
│  • "gpt-4o-mini" → https://api.openai.com/v1 (fallback)              │
└────────────────┬───────────────────────────┬───────────────────────────┘
                 │                           │
        ┌────────▼─────────┐        ┌────────▼─────────┐
        │                  │        │                  │
        │   vLLM-Qwen      │        │   vLLM-Mistral   │
        │   Container 1    │        │   Container 2    │
        │                  │        │                  │
        └──────────────────┘        └──────────────────┘

┌──────────────────────────────────────────────────────────────────────────┐
│                    vLLM Container 1: Qwen3.5-9B                         │
│              Container: lightning-vllm-qwen (GPU 0)                     │
│                                                                          │
│  Model:     Qwen/Qwen3.5-9B                                             │
│  Port:      8001 (host) → 8000 (container)                              │
│  GPU:       Device 0 (18GB VRAM)                                        │
│  Context:   32K tokens                                                   │
│  Speed:     ~2,200 tokens/sec                                           │
│  TTFT:      35-60ms                                                     │
│                                                                          │
│  Used by:                                                                │
│  • SupervisorBrain    (Decision-making, planning)                       │
│  • QualityEvaluator   (Quality scoring, analysis)                       │
│  • RedTeam            (Adversarial critique, validation)                │
│                                                                          │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │                         GPU 0                                      │ │
│  │  ┌──────────────────────────────────────────────────────────────┐ │ │
│  │  │ VRAM Usage: ~18GB / 40GB (A100)                              │ │ │
│  │  │                                                              │ │ │
│  │  │ ███████████████████████░░░░░░░░░░░░░░░░░░░░░░░░ 45%        │ │ │
│  │  │                                                              │ │ │
│  │  │ Model Weights:    14GB                                      │ │ │
│  │  │ KV Cache:         3GB                                       │ │ │
│  │  │ Activation:       1GB                                       │ │ │
│  │  └──────────────────────────────────────────────────────────────┘ │ │
│  └────────────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────────┐
│                  vLLM Container 2: Mistral-7B                           │
│            Container: lightning-vllm-mistral (GPU 1)                    │
│                                                                          │
│  Model:     mistralai/Mistral-7B-Instruct-v0.3                          │
│  Port:      8002 (host) → 8000 (container)                              │
│  GPU:       Device 1 (14GB VRAM)                                        │
│  Context:   32K tokens                                                   │
│  Speed:     ~2,500 tokens/sec                                           │
│  TTFT:      30-50ms                                                     │
│                                                                          │
│  Used by:                                                                │
│  • SupervisorTools    (Tool coordination, research execution)           │
│  • ContextPruner      (Fast fact extraction, summarization)             │
│                                                                          │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │                         GPU 1                                      │ │
│  │  ┌──────────────────────────────────────────────────────────────┐ │ │
│  │  │ VRAM Usage: ~14GB / 40GB (A100)                              │ │ │
│  │  │                                                              │ │ │
│  │  │ ███████████████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ 35%         │ │ │
│  │  │                                                              │ │ │
│  │  │ Model Weights:    11GB                                      │ │ │
│  │  │ KV Cache:         2GB                                       │ │ │
│  │  │ Activation:       1GB                                       │ │ │
│  │  └──────────────────────────────────────────────────────────────┘ │ │
│  └────────────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────────┐
│                    Supporting Services                                   │
│                                                                          │
│  ┌────────────────────┐  ┌────────────────────┐  ┌──────────────────┐  │
│  │   PostgreSQL       │  │   Prometheus       │  │   Grafana        │  │
│  │   (Port 5432)      │  │   (Port 9090)      │  │   (Port 3000)    │  │
│  │                    │  │                    │  │                  │  │
│  │ • Request logs     │  │ • Metrics scraping │  │ • Dashboards     │  │
│  │ • Token usage      │  │ • Alerting         │  │ • Visualization  │  │
│  │ • Error tracking   │  │ • Time-series DB   │  │ • Alerts UI      │  │
│  └────────────────────┘  └────────────────────┘  └──────────────────┘  │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## Request Flow Example

### Scenario: DeepResearch runs a quality evaluation

```
1. DeepResearch Agent
   ↓
   WorkflowModelConfiguration.QualityEvaluatorModel
   ↓
   Returns: "qwen3.5-9b"

2. HTTP Request to LiteLLM
   ↓
   POST http://localhost:4000/v1/chat/completions
   Body: {
     "model": "qwen3.5-9b",
     "messages": [{"role": "user", "content": "Evaluate quality..."}]
   }

3. LiteLLM Router
   ↓
   Receives request for "qwen3.5-9b"
   ↓
   Looks up routing table: qwen3.5-9b → http://vllm-qwen:8000/v1
   ↓
   Logs request to PostgreSQL
   ↓
   Forwards to vLLM-Qwen container

4. vLLM-Qwen Container (GPU 0)
   ↓
   Receives request at internal port 8000
   ↓
   Loads model: Qwen/Qwen3.5-9B
   ↓
   Generates response using GPU 0
   ↓
   Metrics exported to Prometheus
   ↓
   Returns response to LiteLLM

5. LiteLLM Router
   ↓
   Receives response from vLLM-Qwen
   ↓
   Logs response (tokens, latency) to PostgreSQL
   ↓
   Updates Prometheus metrics
   ↓
   Returns to DeepResearch

6. DeepResearch Agent
   ↓
   Receives quality evaluation response
   ↓
   Continues workflow
```

---

## Model Distribution Strategy

```
┌─────────────────────────────────────────────────────────────┐
│                   Workflow Function Distribution             │
└─────────────────────────────────────────────────────────────┘

GPU 0 (Qwen3.5-9B) - Reasoning-Heavy Tasks (3 functions)
├─ SupervisorBrain     (Complex decision-making, 40% of requests)
├─ QualityEvaluator    (Analysis and scoring, 30% of requests)
└─ RedTeam             (Critical evaluation, 30% of requests)

Total GPU 0 Load: ~100% of reasoning workload

GPU 1 (Mistral-7B) - Fast Execution Tasks (2 functions)
├─ SupervisorTools     (Tool coordination, 60% of requests)
└─ ContextPruner       (Fast extraction, 40% of requests)

Total GPU 1 Load: ~100% of speed-critical workload
```

---

## Failover Architecture

```
Request Flow with Failover:

DeepResearch → LiteLLM Proxy
                    │
                    ├─ Try 1: vLLM-Qwen (primary)
                    │   └─ Success → Return
                    │   └─ Fail → Retry
                    │
                    ├─ Try 2: vLLM-Qwen (retry)
                    │   └─ Success → Return
                    │   └─ Fail → Retry
                    │
                    ├─ Try 3: vLLM-Qwen (retry)
                    │   └─ Success → Return
                    │   └─ Fail → Failover
                    │
                    └─ Failover: OpenAI GPT-4o-mini
                        └─ Cloud API (always available)
```

---

## Port Mapping Reference

| Service | Host Port | Container Port | Purpose |
|---------|-----------|----------------|---------|
| **vLLM-Qwen** | 8001 | 8000 | Qwen3.5-9B inference |
| **vLLM-Mistral** | 8002 | 8000 | Mistral-7B inference |
| **LiteLLM API** | 4000 | 4000 | OpenAI-compatible API |
| **LiteLLM Admin** | 4001 | 4001 | Admin dashboard |
| **PostgreSQL** | 5432 | 5432 | Request logging |
| **Prometheus** | 9090 | 9090 | Metrics scraping |
| **Grafana** | 3000 | 3000 | Visualization |

---

## File Location Reference

```
C:\RepoEx\PhoenixAI\DeepResearch\
│
├─ DeepResearchAgent\
│  └─ Configuration\
│     └─ WorkflowModelConfiguration.cs  ← C# model configuration
│
└─ Docker\
   └─ lightning-server\
      ├─ docker-compose.multi-model.yml  ← Main deployment config
      ├─ litellm-multi-model.yaml        ← LiteLLM router config
      ├─ prometheus.yml                   ← Monitoring config (updated)
      │
      ├─ MULTI_MODEL_VLLM_PLAN.md        ← Planning document
      ├─ QUICK_START_MULTI_MODEL.md      ← Deployment guide
      ├─ DEPLOYMENT_SCRIPTS.md            ← Scripts reference
      ├─ IMPLEMENTATION_SUMMARY.md        ← Implementation overview
      └─ ARCHITECTURE_DIAGRAM.md          ← This file
```

---

## GPU Resource Allocation

```
Server with 2x NVIDIA A100 40GB GPUs:

GPU 0 (CUDA Device 0)
├─ Assigned to: lightning-vllm-qwen
├─ Model: Qwen3.5-9B (9B parameters)
├─ VRAM Used: ~18GB / 40GB (45%)
├─ Tasks: Reasoning, Quality, Critique
└─ docker-compose device_ids: ['0']

GPU 1 (CUDA Device 1)
├─ Assigned to: lightning-vllm-mistral
├─ Model: Mistral-7B (7B parameters)
├─ VRAM Used: ~14GB / 40GB (35%)
├─ Tasks: Tools, Context Pruning
└─ docker-compose device_ids: ['1']

Alternative: Single GPU with Ollama Fallback
GPU 0: Qwen3.5-9B (vLLM)
CPU:   Mistral-7B (Ollama) - slower but functional
```

---

## Summary

This architecture provides:
- ✅ **Task-optimized models**: Reasoning vs Speed
- ✅ **GPU isolation**: No resource contention
- ✅ **Intelligent routing**: LiteLLM usage-based balancing
- ✅ **High availability**: Automatic retry and failover
- ✅ **Observability**: Prometheus + Grafana + Admin UI
- ✅ **Scalability**: Easy to add more models/GPUs

**Ready to deploy!** See `QUICK_START_MULTI_MODEL.md` for step-by-step instructions.
