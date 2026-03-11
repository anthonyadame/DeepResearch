# VERL (RL from Human Feedback) Deployment Guide

## 📊 Overview

This guide covers deploying and using VERL (Verification and Reinforcement Learning) for RLHF (Reinforcement Learning from Human Feedback) fine-tuning of language models using PPO (Proximal Policy Optimization).

**VERL** is Microsoft's framework for efficient RL training of LLMs with:
- **PPO Algorithm** - Proven RL algorithm for LLM alignment
- **Ray-based Distribution** - Scalable multi-GPU/multi-node training  
- **vLLM Integration** - Fast inference during rollout generation
- **Lightning Store Integration** - Persistent storage for training data

---

## 🏗️ Architecture

```
┌────────────────────────────────────────────────────────────┐
│                Lightning Server (FastAPI)                  │
│  ┌──────────────────────────────────────────────────────┐ │
│  │  VERL Training Manager                               │ │
│  │  - PPO Trainer                                       │ │
│  │  - Ray distributed workers                           │ │
│  │  - Checkpoint management                             │ │
│  └──────────────────────────────────────────────────────┘ │
└──────┬──────────────────────────────────────────────┬─────┘
       │                                              │
       ↓                                              ↓
┌──────────────┐                              ┌──────────────┐
│ Ray Cluster  │                              │ Lightning    │
│  Workers     │                              │  Store       │
│              │                              │ (MongoDB)    │
│ ┌──────────┐ │                              └──────────────┘
│ │ Actor    │ │  ← Policy model
│ │ Worker   │ │
│ └──────────┘ │
│ ┌──────────┐ │
│ │ Rollout  │ │  ← Reference model (KL div)
│ │ Worker   │ │
│ └──────────┘ │
│ ┌──────────┐ │
│ │ Critic   │ │  ← Value function
│ │ Worker   │ │
│ └──────────┘ │
│ ┌──────────┐ │
│ │ Reward   │ │  ← Reward model
│ │ Worker   │ │
│ └──────────┘ │
└──────┬───────┘
       ↓
┌──────────────┐
│  vLLM Server │  ← Fast inference
│  (GPU)       │
└──────────────┘
```

---

## 🚀 Quick Start

### Prerequisites

**Required:**
- Docker & Docker Compose  
- NVIDIA GPU(s) with CUDA support (minimum 1x A100 40GB or 2x A6000)
- NVIDIA Container Toolkit
- At least 64GB RAM
- MongoDB replica set (for Lightning Store)
- vLLM server running

**Recommended:**
- 2+ GPUs for distributed training
- 128GB+ RAM for large models
- NVMe SSD for fast checkpoint saving

### Enable VERL Training

```bash
cd Docker/lightning-server

# Edit .env to enable VERL
cat >> .env <<EOF
# VERL Training Configuration
VERL_ENABLED=true
VERL_TRAIN_BATCH_SIZE=32
VERL_MAX_PROMPT_LENGTH=4096
VERL_MAX_RESPONSE_LENGTH=2048
VERL_N_ROLLOUTS=4
VERL_PPO_LEARNING_RATE=1e-6
VERL_N_GPUS_PER_NODE=2
VERL_MODEL_PATH=meta-llama/Llama-3.1-8B-Instruct
VERL_TENSOR_PARALLEL_SIZE=1
EOF

# Start Lightning Server with VERL enabled
docker-compose up -d lightning-server

# Verify VERL is enabled
curl http://localhost:9090/health | jq '.verl'
```

---

## ⚙️ Configuration

### Environment Variables

```bash
# Enable/disable VERL training
VERL_ENABLED=true

# Training hyperparameters
VERL_TRAIN_BATCH_SIZE=32          # Batch size for PPO updates
VERL_MAX_PROMPT_LENGTH=4096       # Maximum prompt tokens
VERL_MAX_RESPONSE_LENGTH=2048     # Maximum response tokens
VERL_N_ROLLOUTS=4                 # Rollouts per prompt (for variance reduction)
VERL_PPO_LEARNING_RATE=1e-6       # PPO learning rate

# Model configuration
VERL_MODEL_PATH=meta-llama/Llama-3.1-8B-Instruct  # Actor/policy model
VERL_TENSOR_PARALLEL_SIZE=1       # Tensor parallelism (1 for single GPU)

# Distributed training
VERL_N_GPUS_PER_NODE=2            # GPUs per node for Ray
```

### VERL Configuration (config.py)

```python
class VERLConfig(BaseSettings):
    enabled: bool = True
    train_batch_size: int = 32
    max_prompt_length: int = 4096
    max_response_length: int = 2048
    n_rollouts: int = 4
    ppo_learning_rate: float = 1e-6
    n_gpus_per_node: int = 2
    model_path: str = "meta-llama/Llama-3.1-8B-Instruct"
    tensor_parallel_size: int = 1
```

---

## 🎯 Training Workflow

### 1. Prepare Training Dataset

Create a dataset of prompts for RLHF training:

```python
# prepare_dataset.py
prompts = [
    "Write a Python function to calculate factorial",
    "Explain quantum entanglement in simple terms",
    "Create a REST API endpoint for user authentication",
    # ... more prompts
]

# Save as JSONL (one prompt per line)
import json
with open('training_prompts.jsonl', 'w') as f:
    for prompt in prompts:
        f.write(json.dumps({"prompt": prompt}) + '\n')
```

**Dataset Format**:
```json
{"prompt": "Write a Python function to..."}
{"prompt": "Explain quantum entanglement..."}
{"prompt": "Create a REST API endpoint..."}
```

### 2. Initialize VERL Trainer

```bash
# Initialize trainer with actor and reward models
curl -X POST http://localhost:9090/api/verl/initialize \
  -H "Content-Type: application/json" \
  -d '{
    "actorModel": "meta-llama/Llama-3.1-8B-Instruct",
    "rewardModel": "OpenAssistant/reward-model-deberta-v3-large-v2",
    "outputDir": "./verl_checkpoints"
  }'

# Expected response:
{
  "success": true,
  "actorModel": "meta-llama/Llama-3.1-8B-Instruct",
  "rewardModel": "OpenAssistant/reward-model-deberta-v3-large-v2",
  "outputDir": "./verl_checkpoints",
  "message": "VERL trainer initialized successfully"
}
```

### 3. Start Training

```bash
# Start PPO training
curl -X POST http://localhost:9090/api/verl/train/start \
  -H "Content-Type: application/json" \
  -d '{
    "datasetPath": "/app/data/training_prompts.jsonl",
    "numSteps": 1000,
    "resumeFromCheckpoint": null
  }'

# Expected response:
{
  "success": true,
  "run_id": "verl_run_20240115_143022",
  "num_steps": 1000,
  "dataset": "/app/data/training_prompts.jsonl",
  "started_at": "2024-01-15T14:30:22Z"
}
```

### 4. Monitor Training

```bash
# Get training status
curl http://localhost:9090/api/verl/train/status

# Expected response:
{
  "active": true,
  "stats": {
    "total_steps": 342,
    "total_episodes": 5472,
    "best_reward": 0.78,
    "started_at": "2024-01-15T14:30:22Z",
    "last_checkpoint": "checkpoint_step_300.pt"
  },
  "config": {
    "batch_size": 32,
    "learning_rate": 1e-6,
    "n_rollouts": 4
  }
}
```

### 5. Stop Training

```bash
# Stop active training
curl -X POST http://localhost:9090/api/verl/train/stop

# Expected response:
{
  "success": true,
  "stopped_at": "2024-01-15T16:45:10Z",
  "stats": {
    "total_steps": 1000,
    "total_episodes": 16000,
    "best_reward": 0.85,
    "started_at": "2024-01-15T14:30:22Z",
    "last_checkpoint": "checkpoint_final.pt"
  }
}
```

---

## 📈 PPO Algorithm Details

### How PPO Works for RLHF

**1. Rollout Generation**:
- Actor model generates responses to prompts
- Multiple rollouts per prompt (for variance reduction)
- Responses stored with log probabilities

**2. Reward Computation**:
- Reward model scores each response
- KL divergence penalty (to keep close to reference model)
- Final reward = reward_score - β * KL_divergence

**3. Advantage Estimation**:
- Critic model predicts value for each state
- Advantages computed using GAE (Generalized Advantage Estimation)

**4. PPO Update**:
- Clipped surrogate objective prevents large policy updates
- Value function updated with MSE loss
- Entropy bonus encourages exploration

**Training Loop**:
```
for step in range(num_steps):
    # 1. Rollout phase
    prompts = sample_prompts(batch_size)
    responses, log_probs = actor.generate(prompts)
    
    # 2. Reward computation
    rewards = reward_model(prompts, responses)
    ref_log_probs = reference_model(prompts, responses)
    kl_penalty = KL(log_probs, ref_log_probs)
    final_rewards = rewards - kl_coef * kl_penalty
    
    # 3. Advantage estimation
    values = critic(prompts, responses)
    advantages = compute_advantages(final_rewards, values)
    
    # 4. PPO update
    for epoch in range(ppo_epochs):
        loss = compute_ppo_loss(log_probs, advantages)
        optimizer.step(loss)
```

### Key Hyperparameters

| Parameter | Description | Typical Value |
|-----------|-------------|---------------|
| `learning_rate` | PPO learning rate | 1e-6 to 1e-5 |
| `batch_size` | Number of prompts per batch | 16 to 64 |
| `n_rollouts` | Rollouts per prompt | 4 to 8 |
| `kl_coef` | KL divergence penalty | 0.01 to 0.1 |
| `clip_ratio` | PPO clip ratio | 0.1 to 0.3 |
| `value_loss_coef` | Value function loss weight | 0.5 to 1.0 |
| `entropy_coef` | Entropy bonus weight | 0.01 to 0.1 |

---

## 🔧 Advanced Configuration

### Multi-GPU Training

**Tensor Parallelism** (single model across multiple GPUs):
```bash
VERL_TENSOR_PARALLEL_SIZE=2
VERL_N_GPUS_PER_NODE=2
```

**Data Parallelism** (multiple model replicas):
```bash
# Ray will automatically distribute across available GPUs
VERL_N_GPUS_PER_NODE=4
```

### Multi-Node Training

**Ray Cluster Setup**:
```bash
# Head node
ray start --head --port=6379 --num-gpus=2

# Worker nodes
ray start --address='head-node-ip:6379' --num-gpus=2
```

**VERL Configuration**:
```python
verl_manager = VERLTrainingManager(
    config=config.verl,
    lightning_store=lightning_store,
    enable_ray=True
)
```

### Custom Reward Model

**Train a reward model**:
```python
from transformers import AutoModelForSequenceClassification, AutoTokenizer

# Load base model
model = AutoModelForSequenceClassification.from_pretrained(
    "microsoft/deberta-v3-large",
    num_labels=1  # Single score output
)

# Train on preference dataset (chosen vs rejected)
# ... training code ...

# Save reward model
model.save_pretrained("./my_reward_model")
```

**Use custom reward model**:
```bash
curl -X POST http://localhost:9090/api/verl/initialize \
  -H "Content-Type: application/json" \
  -d '{
    "actorModel": "meta-llama/Llama-3.1-8B-Instruct",
    "rewardModel": "./my_reward_model",
    "outputDir": "./verl_checkpoints"
  }'
```

---

## 📊 Monitoring & Metrics

### Training Metrics

**Key Metrics to Track**:
- **Average Reward**: Mean reward across rollouts (should increase)
- **KL Divergence**: Distance from reference model (should stay bounded)
- **Advantage Mean**: Should converge to ~0
- **Policy Loss**: PPO clipped objective loss
- **Value Loss**: Critic MSE loss
- **Explained Variance**: How well critic predicts returns (0-1, higher is better)

### Lightning Store Integration

Training metrics are automatically logged to Lightning Store:

```bash
# Query training rollouts
curl http://localhost:9090/api/rollouts?mode=train&limit=100

# View training metrics in dashboard
open http://localhost:9090/dashboard
```

### Grafana Dashboards

Add VERL metrics to Grafana:

```yaml
# prometheus.yml
scrape_configs:
  - job_name: 'verl-training'
    static_configs:
      - targets: ['lightning-server:9091']
        labels:
          service: 'verl'
```

**Dashboard Panels**:
- Average reward over time
- KL divergence over time
- Policy/value loss curves
- Training throughput (steps/sec)

---

## 🧪 Testing & Evaluation

### Evaluate Trained Model

```python
# load_and_test.py
import torch
from transformers import AutoModelForCausalLM, AutoTokenizer

# Load trained model
model = AutoModelForCausalLM.from_pretrained("./verl_checkpoints/checkpoint_final")
tokenizer = AutoTokenizer.from_pretrained("meta-llama/Llama-3.1-8B-Instruct")

# Test generation
prompt = "Write a Python function to calculate factorial"
inputs = tokenizer(prompt, return_tensors="pt")
outputs = model.generate(**inputs, max_new_tokens=512)
response = tokenizer.decode(outputs[0], skip_special_tokens=True)

print(response)
```

### A/B Testing

Compare RLHF-trained model vs base model:

```python
# compare_models.py
from transformers import pipeline

base_model = pipeline("text-generation", model="meta-llama/Llama-3.1-8B-Instruct")
rlhf_model = pipeline("text-generation", model="./verl_checkpoints/checkpoint_final")

prompt = "Explain quantum entanglement"

base_response = base_model(prompt, max_new_tokens=256)[0]['generated_text']
rlhf_response = rlhf_model(prompt, max_new_tokens=256)[0]['generated_text']

print("Base model response:")
print(base_response)
print("\nRLHF model response:")
print(rlhf_response)
```

---

## 🐛 Troubleshooting

### Issue: Ray initialization fails

**Symptoms**: `RuntimeError: ray.init() failed`

**Solutions**:
```bash
# 1. Check Ray is installed
pip install "ray[default]"

# 2. Verify GPU access
nvidia-smi

# 3. Start Ray manually
ray start --head --num-gpus=2

# 4. Check Ray status
ray status
```

### Issue: OOM during training

**Symptoms**: CUDA out of memory errors

**Solutions**:
```bash
# 1. Reduce batch size
VERL_TRAIN_BATCH_SIZE=16

# 2. Reduce sequence lengths
VERL_MAX_PROMPT_LENGTH=2048
VERL_MAX_RESPONSE_LENGTH=1024

# 3. Use gradient checkpointing
# (requires code modification in verl_manager.py)

# 4. Enable tensor parallelism
VERL_TENSOR_PARALLEL_SIZE=2
```

### Issue: Training diverges (reward decreases)

**Symptoms**: Average reward drops, KL divergence explodes

**Solutions**:
```bash
# 1. Lower learning rate
VERL_PPO_LEARNING_RATE=5e-7

# 2. Increase KL coefficient (in code)
kl_coef = 0.1  # Stronger KL penalty

# 3. Reduce clip ratio (in code)
clip_ratio = 0.1  # More conservative updates

# 4. Use smaller rollout count
VERL_N_ROLLOUTS=2
```

### Issue: Slow training throughput

**Symptoms**: < 10 steps/minute

**Solutions**:
```bash
# 1. Increase batch size (if GPU memory allows)
VERL_TRAIN_BATCH_SIZE=64

# 2. Use vLLM for faster rollout generation
# (already integrated in verl_manager.py)

# 3. Enable tensor parallelism
VERL_TENSOR_PARALLEL_SIZE=2

# 4. Use larger GPUs or more workers
VERL_N_GPUS_PER_NODE=4
```

---

## 📚 Additional Resources

### VERL Documentation
- [VERL GitHub](https://github.com/volcengine/verl)
- [VERL Paper](https://arxiv.org/abs/2405.12781)
- [Ray Documentation](https://docs.ray.io/)

### PPO & RLHF Papers
- [Proximal Policy Optimization (PPO)](https://arxiv.org/abs/1707.06347)
- [InstructGPT (RLHF)](https://arxiv.org/abs/2203.02155)
- [Constitutional AI](https://arxiv.org/abs/2212.08073)

### Related Tools
- [TRL (Transformer Reinforcement Learning)](https://github.com/huggingface/trl)
- [OpenRLHF](https://github.com/OpenLLMAI/OpenRLHF)
- [DeepSpeed-Chat](https://github.com/microsoft/DeepSpeed/tree/master/blogs/deepspeed-chat)

---

## 📝 Summary Checklist

✅ **VERL Configuration**
- [x] VERL enabled in environment variables
- [x] Ray cluster initialized
- [x] GPU resources allocated
- [x] MongoDB Lightning Store connected

✅ **Training Setup**
- [x] Training dataset prepared (JSONL format)
- [x] Actor model selected
- [x] Reward model trained/selected
- [x] Output directory configured

✅ **Training Workflow**
- [x] Trainer initialized via `/api/verl/initialize`
- [x] Training started via `/api/verl/train/start`
- [x] Monitoring active via `/api/verl/train/status`
- [x] Checkpoints saved periodically

✅ **Monitoring**
- [x] Training metrics logged to Lightning Store
- [x] Grafana dashboards configured
- [x] Prometheus scraping VERL metrics

✅ **Evaluation**
- [x] Model checkpoints loadable
- [x] Trained model tested on new prompts
- [x] A/B testing vs base model

---

**Next Steps:**
- Prepare your preference dataset (prompts + chosen/rejected pairs)
- Train a reward model or use existing one
- Start VERL training: `curl -X POST http://localhost:9090/api/verl/initialize`
- Monitor training progress via Grafana or `/api/verl/train/status`
- Evaluate trained model on held-out test set
