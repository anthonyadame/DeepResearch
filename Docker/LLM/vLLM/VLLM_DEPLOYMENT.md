# vLLM & LLM Proxy Deployment Guide

## 📊 Overview

This guide covers deploying and configuring the LLM serving infrastructure for Lightning Server:

- **vLLM** - Primary LLM serving engine with OpenAI-compatible API
- **LiteLLM Proxy** - Unified router supporting multiple LLM backends
- **PostgreSQL** - Request logging and analytics for LiteLLM
- **Ollama** - Optional local LLM for development

---

## 🏗️ Architecture

```
┌──────────────────────────────────────────────────────────┐
│                   Lightning Server                       │
│  ┌────────────┐                                          │
│  │  API       │  LLM requests                            │
│  │  Endpoints │──────────┐                               │
│  └────────────┘          │                               │
└─────────────────────────┼───────────────────────────────┘
                          ↓
                  ┌───────────────┐
                  │ LiteLLM Proxy │  (Port 4000)
                  │  Router       │
                  └───────┬───────┘
                          ├──────────────┬──────────────┬─────────
                          ↓              ↓              ↓
                   ┌──────────┐   ┌──────────┐  ┌──────────┐
                   │ vLLM     │   │ OpenAI   │  │ Anthropic│
                   │ Server   │   │ API      │  │ API      │
                   │ (8000)   │   │ (Cloud)  │  │ (Cloud)  │
                   └──────────┘   └──────────┘  └──────────┘
                        │
                   ┌────┴────┐
                   │  GPU    │
                   │ (CUDA)  │
                   └─────────┘
```

---

## 🚀 Quick Start

### Prerequisites

**Required:**
- Docker & Docker Compose
- NVIDIA GPU with CUDA support (for vLLM)
- NVIDIA Container Toolkit installed
- At least 16GB GPU memory (for Llama-3.1-8B)
- At least 32GB RAM

**Verify GPU access:**
```bash
# Test NVIDIA Docker runtime
docker run --rm --gpus all nvidia/cuda:12.4.0-base-ubuntu22.04 nvidia-smi

# Expected output: GPU details and CUDA version
```

### Start LLM Serving Stack

```bash
cd Docker/lightning-server

# Start vLLM + LiteLLM Proxy + PostgreSQL
docker-compose --profile llm-serving up -d

# Check service health
docker-compose ps

# View vLLM logs
docker logs -f lightning-vllm

# View LiteLLM logs
docker logs -f lightning-litellm
```

### Verify Deployment

```bash
# 1. Check vLLM health
curl http://localhost:8000/health
# Expected: {"status":"ok"}

# 2. Check vLLM models
curl http://localhost:8000/v1/models
# Expected: {"data":[{"id":"meta-llama/Llama-3.1-8B-Instruct",...}]}

# 3. Check LiteLLM Proxy health
curl http://localhost:4000/health
# Expected: {"status":"healthy"}

# 4. Test LLM completion
curl http://localhost:4000/v1/chat/completions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer sk-1234" \
  -d '{
    "model": "llama-3.1-8b",
    "messages": [{"role": "user", "content": "Hello!"}]
  }'
```

---

## 📊 Quick Model Comparison

| Model | Size | GPU | Context | Speed | Best For | HuggingFace Path |
|-------|------|-----|---------|-------|----------|------------------|
| **Mistral-7B-v0.3** | 7B | 14GB | 32K | ⚡⚡⚡ Fast | General chat, quick responses | `mistralai/Mistral-7B-Instruct-v0.3` |
| **Qwen2.5-7B** | 7B | 14GB | 32K-128K | ⚡⚡ Fast | Long docs, multilingual | `Qwen/Qwen2.5-7B-Instruct` |
| **Qwen3.5-9B** | 9B | 18GB | 32K | ⚡⚡⚡ Fast | Latest Qwen, improved reasoning | `Qwen/Qwen3.5-9B` |
| **GPT-OSS-20B** | 20B | 40GB | 8K | ⚡⚡ Fast | General purpose, research | `openai/gpt-oss-20b` |
| **DeepSeek-Coder-33B** | 33B | 70GB | 16K | ⚡ Moderate | Code generation | `deepseek-ai/deepseek-coder-33b-instruct` |
| **Llama-3.1-8B** | 8B | 16GB | 8K | ⚡⚡ Fast | Balanced quality/speed | `meta-llama/Llama-3.1-8B-Instruct` |
| **Llama-3.1-70B** | 70B | 140GB | 8K | ⚡ Slow | Maximum quality | `meta-llama/Llama-3.1-70B-Instruct` |

**Legend:**
- ⚡⚡⚡ = 2000+ tokens/sec
- ⚡⚡ = 800-2000 tokens/sec  
- ⚡ = 500-800 tokens/sec

**Quick Selection:**
- 🚀 **Need Speed?** → Mistral-7B-v0.3
- 📄 **Long Documents?** → Qwen2.5-7B (128K context)
- 💻 **Code Tasks?** → DeepSeek-Coder-33B
- 🌍 **Multilingual?** → Qwen2.5-7B
- 🎯 **Best Balance?** → Llama-3.1-8B
- 👑 **Best Quality?** → Llama-3.1-70B

---

## ⚙️ Configuration

### vLLM Server Configuration

**Environment Variables** (set in `docker-compose.yml`):

```yaml
environment:
  # Model selection
  - MODEL_NAME=meta-llama/Llama-3.1-8B-Instruct
  
  # Performance tuning
  - TENSOR_PARALLEL_SIZE=1  # Number of GPUs for tensor parallelism
  - GPU_MEMORY_UTILIZATION=0.9  # GPU memory utilization (0.0-1.0)
  - MAX_MODEL_LEN=8192  # Maximum sequence length
  - MAX_NUM_SEQS=256  # Maximum concurrent sequences
  
  # Optimizations
  - ENABLE_PREFIX_CACHING=true  # Enable KV cache sharing
  - DISABLE_LOG_STATS=false  # Log statistics
  
  # HuggingFace (for gated models like Llama)
  - HF_TOKEN=<your-huggingface-token>
```

**Command-Line Arguments:**

```yaml
command:
  - --model
  - meta-llama/Llama-3.1-8B-Instruct
  - --tensor-parallel-size
  - "1"
  - --gpu-memory-utilization
  - "0.9"
  - --max-model-len
  - "8192"
  - --max-num-seqs
  - "256"
  - --enable-prefix-caching
  - --port
  - "8000"
  - --host
  - "0.0.0.0"
```

### LiteLLM Proxy Configuration

**Configuration File**: `litellm-config.yaml`

Key sections:

1. **Model List** - Define available models:
```yaml
model_list:
  - model_name: llama-3.1-8b
    litellm_params:
      model: openai/llama-3.1-8b
      api_base: http://vllm-server:8000/v1
      api_key: "EMPTY"
      custom_llm_provider: openai
```

2. **Router Settings** - Load balancing and failover:
```yaml
router_settings:
  routing_strategy: usage-based-routing
  num_retries: 3
  allowed_fails: 3
  cooldown_time: 60
```

3. **General Settings** - API key, logging, metrics:
```yaml
general_settings:
  master_key: "sk-1234"  # Change in production!
  database_url: "postgresql://litellm:litellm@postgres:5432/litellm"
  json_logs: true
```

---

## 📈 Model Selection

### Supported Models (vLLM)

| Model | Size | GPU Memory | Context Length | Notes |
|-------|------|------------|----------------|-------|
| Llama-3.1-8B-Instruct | 8B | 16GB | 8K | Default, recommended for development |
| Llama-3.1-70B-Instruct | 70B | 140GB (2x A100) | 8K | Requires multi-GPU (tensor parallelism) |
| Mistral-7B-Instruct-v0.3 | 7B | 14GB | 32K | Fast, efficient, extended context |
| Qwen2.5-7B-Instruct | 7B | 14GB | 32K | Long context, multilingual support |
| Qwen3.5-9B | 9B | 18GB | 32K | Latest Qwen, improved reasoning, fast |
| GPT-OSS-20B | 20B | 40GB (A100) | 8K | General purpose, research, OpenAI baseline |
| DeepSeek-Coder-33B-Instruct | 33B | 70GB (A100) | 16K | Code generation specialist |
| CodeLlama-34B-Instruct | 34B | 70GB (A100) | 16K | Code generation |

### Model-Specific Configurations

#### Mistral-7B-Instruct-v0.3

**Best for:** General purpose, fast inference, extended context (32K tokens)

**Configuration:**
```yaml
# docker-compose.yml
environment:
  - MODEL_NAME=mistralai/Mistral-7B-Instruct-v0.3
  - GPU_MEMORY_UTILIZATION=0.9
  - MAX_MODEL_LEN=32768
  - MAX_NUM_SEQS=256
  - TENSOR_PARALLEL_SIZE=1

command:
  - --model
  - mistralai/Mistral-7B-Instruct-v0.3
  - --gpu-memory-utilization
  - "0.9"
  - --max-model-len
  - "32768"  # Full 32K context
  - --max-num-seqs
  - "256"
  - --enable-prefix-caching
  - --trust-remote-code  # Required for some Mistral variants
```

**Performance:**
- **GPU:** 1x A100 (40GB) or RTX 4090 (24GB)
- **Throughput:** ~2,500 tokens/sec
- **TTFT:** 30-50ms
- **Best use cases:** Conversational AI, summarization, general QA

#### Qwen2.5-7B-Instruct

**Best for:** Long context (32K), multilingual, reasoning tasks

**Configuration:**
```yaml
# docker-compose.yml
environment:
  - MODEL_NAME=Qwen/Qwen2.5-7B-Instruct
  - GPU_MEMORY_UTILIZATION=0.9
  - MAX_MODEL_LEN=32768
  - MAX_NUM_SEQS=128  # Lower for long context
  - TENSOR_PARALLEL_SIZE=1

command:
  - --model
  - Qwen/Qwen2.5-7B-Instruct
  - --gpu-memory-utilization
  - "0.9"
  - --max-model-len
  - "32768"  # Supports up to 128K with rope scaling
  - --max-num-seqs
  - "128"
  - --enable-prefix-caching
  - --trust-remote-code  # Required for Qwen models
  - --rope-scaling
  - '{"type":"dynamic","factor":4.0}'  # For extended context beyond 32K
```

**Performance:**
- **GPU:** 1x A100 (40GB) or RTX 4090 (24GB)
- **Throughput:** ~2,000 tokens/sec (standard context), ~800 tokens/sec (128K context)
- **TTFT:** 40-70ms
- **Best use cases:** Long document analysis, multilingual chat, complex reasoning

**Special Features:**
- Supports English, Chinese, Japanese, Korean, and 20+ languages
- Dynamic RoPE scaling for contexts up to 128K tokens
- Strong code understanding capabilities

#### Qwen3.5-9B

**Best for:** Latest generation Qwen model with improved reasoning and performance

**Configuration:**
```yaml
# docker-compose.yml
environment:
  - MODEL_NAME=Qwen/Qwen3.5-9B
  - GPU_MEMORY_UTILIZATION=0.9
  - MAX_MODEL_LEN=32768
  - MAX_NUM_SEQS=256
  - TENSOR_PARALLEL_SIZE=1

command:
  - --model
  - Qwen/Qwen3.5-9B
  - --gpu-memory-utilization
  - "0.9"
  - --max-model-len
  - "32768"
  - --max-num-seqs
  - "256"
  - --enable-prefix-caching
  - --trust-remote-code  # Required for Qwen models
```

**Performance:**
- **GPU:** 1x A100 (40GB) or RTX 4090 (24GB)
- **Throughput:** ~2,200 tokens/sec (32K context)
- **TTFT:** 35-60ms
- **Best use cases:** General QA, reasoning tasks, content generation, multilingual applications

**Special Features:**
- Latest Qwen 3.5 generation with improved architecture
- Enhanced reasoning capabilities over Qwen 2.5
- Native 32K context window
- Multilingual support (English, Chinese, and 20+ languages)
- Better instruction following and task comprehension

#### DeepSeek-Coder-33B-Instruct

**Best for:** Code generation, code review, technical documentation

**Configuration:**
```yaml
# docker-compose.yml
environment:
  - MODEL_NAME=deepseek-ai/deepseek-coder-33b-instruct
  - GPU_MEMORY_UTILIZATION=0.9
  - MAX_MODEL_LEN=16384
  - MAX_NUM_SEQS=64  # Lower for larger model
  - TENSOR_PARALLEL_SIZE=2  # Use 2 GPUs if available

command:
  - --model
  - deepseek-ai/deepseek-coder-33b-instruct
  - --gpu-memory-utilization
  - "0.9"
  - --max-model-len
  - "16384"
  - --max-num-seqs
  - "64"
  - --enable-prefix-caching
  - --tensor-parallel-size
  - "2"  # Multi-GPU recommended
  - --trust-remote-code
```

**Performance:**
- **GPU:** 2x A100 (40GB each) or 1x A100 (80GB)
- **Throughput:** ~800 tokens/sec (with 2x GPU TP)
- **TTFT:** 80-120ms
- **Best use cases:** Code completion, debugging, code explanation, multi-language code

**Special Features:**
- Trained on 87% code, 13% natural language
- Supports 80+ programming languages
- Fill-in-the-middle (FIM) support for code completion
- Excellent at following coding conventions

#### GPT-OSS-20B

**Best for:** General purpose tasks, research, balanced quality/cost

**Configuration:**
```yaml
# docker-compose.yml
environment:
  - MODEL_NAME=openai/gpt-oss-20b
  - GPU_MEMORY_UTILIZATION=0.9
  - MAX_MODEL_LEN=8192
  - MAX_NUM_SEQS=128
  - TENSOR_PARALLEL_SIZE=1

command:
  - --model
  - openai/gpt-oss-20b
  - --gpu-memory-utilization
  - "0.9"
  - --max-model-len
  - "8192"
  - --max-num-seqs
  - "128"
  - --enable-prefix-caching
  - --trust-remote-code
```

**Performance:**
- **GPU:** 1x A100 (40GB)
- **Throughput:** ~1,200 tokens/sec
- **TTFT:** 50-80ms
- **Best use cases:** General QA, content generation, research applications, baseline comparisons

**Special Features:**
- OpenAI's open-source 20B model released for research
- Good balance between quality and resource requirements
- Suitable for tasks requiring more capacity than 7B models
- Useful as baseline for benchmarking custom models

### Changing Models

**Option 1: Environment Variable** (simplest)
```bash
# Edit docker-compose.yml to change model
# Example: Switch to Mistral-7B
environment:
  - MODEL_NAME=mistralai/Mistral-7B-Instruct-v0.3

# Example: Switch to Qwen2.5-7B
environment:
  - MODEL_NAME=Qwen/Qwen2.5-7B-Instruct

# Example: Switch to Qwen3.5-9B
environment:
  - MODEL_NAME=Qwen/Qwen3.5-9B

# Example: Switch to GPT-OSS-20B
environment:
  - MODEL_NAME=openai/gpt-oss-20b

# Example: Switch to DeepSeek-Coder-33B
environment:
  - MODEL_NAME=deepseek-ai/deepseek-coder-33b-instruct

# Restart vLLM to apply changes
docker-compose restart vllm-server
```

**Option 2: Command Override** (for testing)
```bash
# Stop existing vLLM container
docker-compose up -d --scale vllm-server=0

# Mistral-7B with extended context
docker run --rm --gpus all \
  -p 8000:8000 \
  -v vllm-cache:/root/.cache/huggingface \
  vllm/vllm-openai:v0.6.3.post1 \
  --model mistralai/Mistral-7B-Instruct-v0.3 \
  --max-model-len 32768 \
  --enable-prefix-caching \
  --trust-remote-code

# Qwen2.5-7B with 128K context
docker run --rm --gpus all \
  -p 8000:8000 \
  -v vllm-cache:/root/.cache/huggingface \
  vllm/vllm-openai:v0.6.3.post1 \
  --model Qwen/Qwen2.5-7B-Instruct \
  --max-model-len 131072 \
  --rope-scaling '{"type":"dynamic","factor":4.0}' \
  --enable-prefix-caching \
  --trust-remote-code

# Qwen3.5-9B with 32K context
docker run --rm --gpus all \
  -p 8000:8000 \
  -v vllm-cache:/root/.cache/huggingface \
  vllm/vllm-openai:v0.6.3.post1 \
  --model Qwen/Qwen3.5-9B \
  --max-model-len 32768 \
  --enable-prefix-caching \
  --trust-remote-code

# GPT-OSS-20B for general purpose
docker run --rm --gpus all \
  -p 8000:8000 \
  -v vllm-cache:/root/.cache/huggingface \
  vllm/vllm-openai:v0.6.3.post1 \
  --model openai/gpt-oss-20b \
  --max-model-len 8192 \
  --enable-prefix-caching \
  --trust-remote-code

# DeepSeek-Coder-33B with 2 GPUs
docker run --rm --gpus all \
  -p 8000:8000 \
  -v vllm-cache:/root/.cache/huggingface \
  vllm/vllm-openai:v0.6.3.post1 \
  --model deepseek-ai/deepseek-coder-33b-instruct \
  --tensor-parallel-size 2 \
  --max-model-len 16384 \
  --enable-prefix-caching \
  --trust-remote-code
```

### Model Selection Guide

**Choose your model based on your use case:**

| Use Case | Recommended Model | Reason |
|----------|------------------|---------|
| **General Chat/QA** | Mistral-7B-Instruct-v0.3 | Fast, efficient, 32K context |
| **Long Documents** | Qwen2.5-7B-Instruct | 32K-128K context, strong reasoning |
| **Code Generation** | DeepSeek-Coder-33B | Specialized for code, 80+ languages |
| **Multilingual** | Qwen3.5-9B | Latest Qwen, improved reasoning, 32K context |
| **Research/Baseline** | GPT-OSS-20B | Mid-size model, good quality/cost balance |
| **Production (High Load)** | Llama-3.1-8B-Instruct | Balanced performance/quality |
| **Best Quality** | Llama-3.1-70B-Instruct | Highest quality (requires 2x GPU) |
| **Development/Testing** | Mistral-7B-Instruct-v0.3 | Fast iteration, low resource |

**Resource Requirements Summary:**

| Model | Min GPU | Recommended GPU | RAM | Best For |
|-------|---------|-----------------|-----|----------|
| Mistral-7B | RTX 3090 (24GB) | RTX 4090 / A100 (40GB) | 32GB | Speed + quality balance |
| Qwen2.5-7B | RTX 3090 (24GB) | A100 (40GB) | 32GB | Long context tasks |
| Qwen3.5-9B | RTX 3090 (24GB) | A100 (40GB) | 32GB | Latest Qwen, improved reasoning |
| GPT-OSS-20B | A100 (40GB) | A100 (40GB) | 48GB | Mid-size general purpose |
| DeepSeek-Coder-33B | A100 (80GB) or 2x A100 (40GB) | 2x A100 (80GB) | 64GB | Code-heavy workloads |
| Llama-3.1-8B | RTX 3090 (24GB) | A100 (40GB) | 32GB | General purpose |
| Llama-3.1-70B | 2x A100 (80GB) | 4x A100 (80GB) | 128GB | Maximum quality |

---

## 🔧 Performance Tuning

### vLLM Optimization

**1. GPU Memory Utilization**
```yaml
# Lower if OOM errors occur
GPU_MEMORY_UTILIZATION: 0.85  # Default: 0.9

# Monitor GPU memory
nvidia-smi -l 1
```

**2. Tensor Parallelism (Multi-GPU)**
```yaml
# For 70B models, use 2+ GPUs
TENSOR_PARALLEL_SIZE: 2
deploy:
  resources:
    reservations:
      devices:
        - driver: nvidia
          count: 2  # Number of GPUs
          capabilities: [gpu]
```

**3. Sequence Length vs Batch Size**
```yaml
# Longer contexts = fewer concurrent requests
MAX_MODEL_LEN: 4096  # Reduce for higher throughput
MAX_NUM_SEQS: 512    # Increase for more concurrency
```

**4. Prefix Caching** (for repeated prompts)
```bash
# Enable KV cache sharing
--enable-prefix-caching
```

### LiteLLM Routing Strategies

**1. Usage-Based Routing** (default)
```yaml
routing_strategy: usage-based-routing
# Balances load across available models
```

**2. Latency-Based Routing**
```yaml
routing_strategy: latency-based-routing
# Routes to fastest model
```

**3. Cost-Based Routing**
```yaml
routing_strategy: cost-based-routing
# Prioritizes cheaper models (vLLM over OpenAI)
```

---

## 🧪 Testing & Benchmarking

### Load Testing with LiteLLM

```bash
# Install LiteLLM CLI
pip install litellm

# Run benchmark
litellm --test \
  --model llama-3.1-8b \
  --api_base http://localhost:4000 \
  --api_key sk-1234 \
  --num_requests 100 \
  --max_tokens 512
```

### vLLM Benchmarking

```bash
# Clone vLLM repo
git clone https://github.com/vllm-project/vllm.git
cd vllm/benchmarks

# Run throughput benchmark
python benchmark_throughput.py \
  --model meta-llama/Llama-3.1-8B-Instruct \
  --num-prompts 1000 \
  --max-model-len 8192
```

### Expected Performance

**Llama-3.1-8B on A100 (40GB)**:
- **Throughput**: ~2,000 tokens/sec
- **Latency (p50)**: ~50ms per token
- **Latency (p99)**: ~200ms per token
- **Concurrent requests**: 100-200

**Llama-3.1-70B on 2x A100 (80GB each)**:
- **Throughput**: ~500 tokens/sec
- **Latency (p50)**: ~100ms per token
- **Concurrent requests**: 20-30

---

## 🔍 Monitoring & Observability

### vLLM Metrics

**Prometheus Endpoint**: http://localhost:8000/metrics

Key metrics:
- `vllm:num_requests_running` - Active requests
- `vllm:num_requests_waiting` - Queue depth
- `vllm:gpu_cache_usage_perc` - KV cache utilization
- `vllm:time_to_first_token_seconds` - TTFT latency
- `vllm:time_per_output_token_seconds` - Token generation latency

### LiteLLM Admin UI

**URL**: http://localhost:4001

Features:
- Real-time request monitoring
- Model health status
- Cost tracking
- Request/response logs
- Error analytics

### Grafana Dashboards

Add to `prometheus.yml`:
```yaml
scrape_configs:
  - job_name: 'vllm'
    static_configs:
      - targets: ['vllm-server:8000']
        labels:
          service: 'vllm'
  
  - job_name: 'litellm'
    static_configs:
      - targets: ['litellm-proxy:4000']
        labels:
          service: 'litellm'
```

---

## 🔐 Security & Production

### API Key Management

**LiteLLM Proxy**:
```yaml
# Change default master key
general_settings:
  master_key: ${LITELLM_MASTER_KEY}  # Use environment variable
```

**vLLM**:
```bash
# Add API key authentication
command:
  - --api-key
  - ${VLLM_API_KEY}
```

### Rate Limiting

**LiteLLM**:
```yaml
model_list:
  - model_name: llama-3.1-8b
    litellm_params:
      rpm: 1000  # Requests per minute
      tpm: 500000  # Tokens per minute
```

### Network Isolation

**Production docker-compose**:
```yaml
networks:
  lightning-net:
    driver: bridge
    internal: true  # No external access

  public-net:
    driver: bridge

services:
  lightning-server:
    networks:
      - lightning-net
      - public-net  # Expose only Lightning Server
```

---

## 🐛 Troubleshooting

### Issue: vLLM OOM (Out of Memory)

**Symptoms**: vLLM crashes with CUDA OOM error

**Solutions**:
```yaml
# 1. Reduce GPU memory utilization
GPU_MEMORY_UTILIZATION: 0.85

# 2. Reduce max model length
MAX_MODEL_LEN: 4096

# 3. Reduce concurrent sequences
MAX_NUM_SEQS: 128

# 4. Use quantization (4-bit)
command:
  - --quantization
  - awq  # or 'gptq'
```

### Issue: Slow first token latency

**Symptoms**: High time-to-first-token (TTFT)

**Solutions**:
```bash
# 1. Enable prefix caching
--enable-prefix-caching

# 2. Increase tensor parallelism
--tensor-parallel-size 2

# 3. Use flash-attention (default in vLLM)
```

### Issue: LiteLLM connection errors

**Symptoms**: LiteLLM cannot connect to vLLM

**Diagnosis**:
```bash
# Check vLLM health
docker exec lightning-litellm curl http://vllm-server:8000/health

# Check network connectivity
docker exec lightning-litellm ping vllm-server
```

**Solutions**:
- Verify both services are on `lightning-net` network
- Check vLLM is fully started (120s start period)
- Verify model is loaded (check vLLM logs)

### Issue: Model download slow/fails

**Symptoms**: HuggingFace download timeout

**Solutions**:
```bash
# 1. Pre-download model
docker run --rm -v vllm-cache:/cache \
  python:3.11-slim bash -c "
  pip install huggingface_hub && \
  python -c 'from huggingface_hub import snapshot_download; \
  snapshot_download(\"meta-llama/Llama-3.1-8B-Instruct\", cache_dir=\"/cache\")'
  "

# 2. Use HF_TOKEN for gated models
environment:
  - HF_TOKEN=hf_your_token_here

# 3. Use mirror (China)
environment:
  - HF_ENDPOINT=https://hf-mirror.com
```

### Pre-Downloading Models

**For all models, pre-download to avoid startup delays:**

```bash
# Mistral-7B-Instruct-v0.3
docker run --rm -v vllm-cache:/cache python:3.11-slim bash -c "
  pip install huggingface_hub && \
  python -c 'from huggingface_hub import snapshot_download; \
  snapshot_download(\"mistralai/Mistral-7B-Instruct-v0.3\", cache_dir=\"/cache\")'
"

# Qwen2.5-7B-Instruct
docker run --rm -v vllm-cache:/cache python:3.11-slim bash -c "
  pip install huggingface_hub && \
  python -c 'from huggingface_hub import snapshot_download; \
  snapshot_download(\"Qwen/Qwen2.5-7B-Instruct\", cache_dir=\"/cache\")'
"

# Qwen3.5-9B
docker run --rm -v vllm-cache:/cache python:3.11-slim bash -c "
  pip install huggingface_hub && \
  python -c 'from huggingface_hub import snapshot_download; \
  snapshot_download(\"Qwen/Qwen3.5-9B\", cache_dir=\"/cache\")'
"

# GPT-OSS-20B
docker run --rm -v vllm-cache:/cache python:3.11-slim bash -c "
  pip install huggingface_hub && \
  python -c 'from huggingface_hub import snapshot_download; \
  snapshot_download(\"openai/gpt-oss-20b\", cache_dir=\"/cache\")'
"

# DeepSeek-Coder-33B-Instruct
docker run --rm -v vllm-cache:/cache python:3.11-slim bash -c "
  pip install huggingface_hub && \
  python -c 'from huggingface_hub import snapshot_download; \
  snapshot_download(\"deepseek-ai/deepseek-coder-33b-instruct\", cache_dir=\"/cache\")'
"

# Llama-3.1-8B-Instruct (requires HF token for gated model)
docker run --rm -v vllm-cache:/cache -e HF_TOKEN=hf_your_token python:3.11-slim bash -c "
  pip install huggingface_hub && \
  python -c 'from huggingface_hub import snapshot_download; \
  snapshot_download(\"meta-llama/Llama-3.1-8B-Instruct\", cache_dir=\"/cache\")'
"
```

---

## 📋 Model Deployment Examples

### Example 1: Deploy Mistral-7B for Fast Inference

**Use case:** Production chatbot with 32K context support

**docker-compose.yml override:**
```yaml
services:
  vllm-server:
    environment:
      - MODEL_NAME=mistralai/Mistral-7B-Instruct-v0.3
      - GPU_MEMORY_UTILIZATION=0.9
      - MAX_MODEL_LEN=32768
      - MAX_NUM_SEQS=256
      - TENSOR_PARALLEL_SIZE=1
    command:
      - --model
      - mistralai/Mistral-7B-Instruct-v0.3
      - --gpu-memory-utilization
      - "0.9"
      - --max-model-len
      - "32768"
      - --max-num-seqs
      - "256"
      - --enable-prefix-caching
      - --trust-remote-code
```

**Expected Performance:**
- Throughput: ~2,500 tokens/sec
- Latency (TTFT): 30-50ms
- GPU: RTX 4090 (24GB) or A100 (40GB)

### Example 2: Deploy Qwen2.5-7B for Long Documents

**Use case:** Document analysis, summarization with 128K context

**docker-compose.yml override:**
```yaml
services:
  vllm-server:
    environment:
      - MODEL_NAME=Qwen/Qwen2.5-7B-Instruct
      - GPU_MEMORY_UTILIZATION=0.85  # Lower for long context
      - MAX_MODEL_LEN=131072  # 128K context
      - MAX_NUM_SEQS=64  # Reduce for memory
      - TENSOR_PARALLEL_SIZE=1
    command:
      - --model
      - Qwen/Qwen2.5-7B-Instruct
      - --gpu-memory-utilization
      - "0.85"
      - --max-model-len
      - "131072"
      - --max-num-seqs
      - "64"
      - --enable-prefix-caching
      - --trust-remote-code
      - --rope-scaling
      - '{"type":"dynamic","factor":4.0}'
```

**Expected Performance:**
- Throughput: ~800 tokens/sec (128K context), ~2,000 tokens/sec (32K)
- Latency (TTFT): 40-70ms (standard), 100-200ms (128K)
- GPU: A100 (40GB) or better

### Example 3: Deploy DeepSeek-Coder-33B for Code Generation

**Use case:** AI coding assistant, code review

**docker-compose.yml override:**
```yaml
services:
  vllm-server:
    environment:
      - MODEL_NAME=deepseek-ai/deepseek-coder-33b-instruct
      - GPU_MEMORY_UTILIZATION=0.9
      - MAX_MODEL_LEN=16384
      - MAX_NUM_SEQS=64
      - TENSOR_PARALLEL_SIZE=2  # Multi-GPU required
    command:
      - --model
      - deepseek-ai/deepseek-coder-33b-instruct
      - --gpu-memory-utilization
      - "0.9"
      - --max-model-len
      - "16384"
      - --max-num-seqs
      - "64"
      - --enable-prefix-caching
      - --tensor-parallel-size
      - "2"
      - --trust-remote-code
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: 2  # Requires 2 GPUs
              capabilities: [gpu]
```

**Expected Performance:**
- Throughput: ~800 tokens/sec (2x GPU)
- Latency (TTFT): 80-120ms
- GPU: 2x A100 (40GB each) or 1x A100 (80GB)

### Example 4: Deploy GPT-OSS-20B for General Purpose

**Use case:** Research applications, baseline comparisons, general QA

**docker-compose.yml override:**
```yaml
services:
  vllm-server:
    environment:
      - MODEL_NAME=openai/gpt-oss-20b
      - GPU_MEMORY_UTILIZATION=0.9
      - MAX_MODEL_LEN=8192
      - MAX_NUM_SEQS=128
      - TENSOR_PARALLEL_SIZE=1
    command:
      - --model
      - openai/gpt-oss-20b
      - --gpu-memory-utilization
      - "0.9"
      - --max-model-len
      - "8192"
      - --max-num-seqs
      - "128"
      - --enable-prefix-caching
      - --trust-remote-code
```

**Expected Performance:**
- Throughput: ~1,200 tokens/sec
- Latency (TTFT): 50-80ms
- GPU: A100 (40GB)

### Example 5: Deploy Qwen3.5-9B for Improved Reasoning

**Use case:** Latest generation Qwen model, general purpose QA with enhanced reasoning

**docker-compose.yml override:**
```yaml
services:
  vllm-server:
    environment:
      - MODEL_NAME=Qwen/Qwen3.5-9B
      - GPU_MEMORY_UTILIZATION=0.9
      - MAX_MODEL_LEN=32768
      - MAX_NUM_SEQS=256
      - TENSOR_PARALLEL_SIZE=1
    command:
      - --model
      - Qwen/Qwen3.5-9B
      - --gpu-memory-utilization
      - "0.9"
      - --max-model-len
      - "32768"
      - --max-num-seqs
      - "256"
      - --enable-prefix-caching
      - --trust-remote-code
```

**Expected Performance:**
- Throughput: ~2,200 tokens/sec
- Latency (TTFT): 35-60ms
- GPU: RTX 4090 (24GB) or A100 (40GB)

### Example 6: Multi-Model Setup with LiteLLM

**Use case:** Route different tasks to specialized models

**litellm-config.yaml:**
```yaml
model_list:
  # Fast general-purpose model
  - model_name: mistral-7b
    litellm_params:
      model: openai/mistral-7b
      api_base: http://vllm-server-1:8000/v1
      api_key: "EMPTY"
    model_info:
      base_model: mistralai/Mistral-7B-Instruct-v0.3
      tags: ["fast", "general"]

  # Long context model
  - model_name: qwen-long
    litellm_params:
      model: openai/qwen-long
      api_base: http://vllm-server-2:8000/v1
      api_key: "EMPTY"
    model_info:
      base_model: Qwen/Qwen2.5-7B-Instruct
      tags: ["long-context", "multilingual"]

  # Code specialist
  - model_name: coder
    litellm_params:
      model: openai/coder
      api_base: http://vllm-server-3:8000/v1
      api_key: "EMPTY"
    model_info:
      base_model: deepseek-ai/deepseek-coder-33b-instruct
      tags: ["code", "technical"]

router_settings:
  routing_strategy: simple-shuffle
  model_group_alias:
    general: ["mistral-7b"]
    documents: ["qwen-long"]
    code: ["coder"]
```

**Deploy 3 separate vLLM instances:**
```bash
# Terminal 1: Mistral for general tasks
docker run --rm --gpus '"device=0"' -p 8001:8000 \
  -v vllm-cache:/root/.cache/huggingface \
  vllm/vllm-openai:v0.6.3.post1 \
  --model mistralai/Mistral-7B-Instruct-v0.3 --trust-remote-code

# Terminal 2: Qwen for long context
docker run --rm --gpus '"device=1"' -p 8002:8000 \
  -v vllm-cache:/root/.cache/huggingface \
  vllm/vllm-openai:v0.6.3.post1 \
  --model Qwen/Qwen2.5-7B-Instruct \
  --max-model-len 131072 \
  --rope-scaling '{"type":"dynamic","factor":4.0}' \
  --trust-remote-code

# Terminal 3: DeepSeek for code
docker run --rm --gpus '"device=2,3"' -p 8003:8000 \
  -v vllm-cache:/root/.cache/huggingface \
  vllm/vllm-openai:v0.6.3.post1 \
  --model deepseek-ai/deepseek-coder-33b-instruct \
  --tensor-parallel-size 2 \
  --trust-remote-code
```

---

## 📚 Additional Resources

### vLLM Documentation
- [Official Docs](https://docs.vllm.ai/)
- [Performance Tuning](https://docs.vllm.ai/en/latest/performance.html)
- [Supported Models](https://docs.vllm.ai/en/latest/models/supported_models.html)

### LiteLLM Documentation
- [Official Docs](https://docs.litellm.ai/)
- [Proxy Configuration](https://docs.litellm.ai/docs/proxy/configs)
- [Routing Strategies](https://docs.litellm.ai/docs/routing)

### Model Repositories
- [HuggingFace Models](https://huggingface.co/models)
- [Llama 3.1](https://huggingface.co/meta-llama/Llama-3.1-8B-Instruct) - Meta's flagship model
- [Mistral-7B](https://huggingface.co/mistralai/Mistral-7B-Instruct-v0.3) - Fast 7B with 32K context
- [Qwen2.5](https://huggingface.co/Qwen/Qwen2.5-7B-Instruct) - Alibaba's multilingual model
- [DeepSeek-Coder](https://huggingface.co/deepseek-ai/deepseek-coder-33b-instruct) - Code specialist

### Model Comparison & Benchmarks
- [LMSYS Chatbot Arena](https://chat.lmsys.org/) - Community-driven model rankings
- [Open LLM Leaderboard](https://huggingface.co/spaces/HuggingFaceH4/open_llm_leaderboard) - Standardized benchmarks
- [vLLM Performance Benchmarks](https://blog.vllm.ai/2024/09/05/perf-update.html) - Official vLLM benchmarks

---

## 📝 Summary Checklist

✅ **vLLM Deployment**
- [x] NVIDIA GPU with CUDA support
- [x] vLLM container running on port 8000
- [x] Model loaded successfully
- [x] Health check passing

✅ **LiteLLM Proxy**
- [x] LiteLLM container running on port 4000
- [x] Configuration file mounted
- [x] PostgreSQL connected for logging
- [x] Routing to vLLM backend

✅ **Integration**
- [x] Lightning Server can connect to LiteLLM Proxy
- [x] End-to-end LLM requests working
- [x] Metrics exposed to Prometheus

✅ **Monitoring**
- [x] vLLM metrics scraping
- [x] LiteLLM admin UI accessible
- [x] Grafana dashboards configured

---

**Next Steps:**
- Test LLM completion endpoint: `curl http://localhost:4000/v1/chat/completions`
- Monitor GPU usage: `nvidia-smi -l 1`
- View LiteLLM admin UI: http://localhost:4001
- Configure Lightning Server to use LiteLLM Proxy for agent tasks
