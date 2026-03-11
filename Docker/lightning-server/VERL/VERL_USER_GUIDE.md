# VERL Training API - User Guide

**Version:** 1.0  
**Last Updated:** 2026-03-09  
**Status:** Production-Ready ✅  

---

## Table of Contents

1. [Quick Start](#quick-start)
2. [API Reference](#api-reference)
3. [Configuration Guide](#configuration-guide)
4. [Workflow Examples](#workflow-examples)
5. [Monitoring & Troubleshooting](#monitoring--troubleshooting)
6. [Best Practices](#best-practices)

---

## Quick Start

### Prerequisites

- Lightning Server running and healthy
- MongoDB connected (3-node replica set)
- Training dataset in JSONL format
- Model available (Qwen/Llama/Mistral families supported)

### 5-Minute Training Job

```powershell
# 1. Create a dataset (JSONL format)
@'
{"prompt": "What is AI?", "response": "AI is artificial intelligence..."}
{"prompt": "Explain ML", "response": "Machine learning is..."}
{"prompt": "What is RL?", "response": "Reinforcement learning is..."}
'@ | Out-File -FilePath training_data.jsonl -Encoding utf8

# 2. Copy to container
docker cp training_data.jsonl research-lightning-server:/app/training_data.jsonl

# 3. Submit training job
$body = @{
    train_dataset = "/app/training_data.jsonl"
    model_path = "Qwen/Qwen2.5-0.5B-Instruct"
    learning_rate = 0.00001
    batch_size = 4
    n_rollouts = 16
    num_steps = 100
} | ConvertTo-Json

$job = Invoke-RestMethod -Uri "http://localhost:8090/verl/train" `
    -Method Post -Body $body -ContentType "application/json"

Write-Host "Job ID: $($job.job_id)"
Write-Host "Status: $($job.status)"

# 4. Monitor progress
Invoke-RestMethod -Uri "http://localhost:8090/verl/jobs/$($job.job_id)" -Method Get
```

---

## API Reference

### Base URL
```
http://localhost:8090
```

### Endpoints

#### 1. **Submit Training Job**

**POST** `/verl/train`

Submit a new VERL training job.

**Request Body:**
```json
{
  "project_name": "my_training_project",
  "train_dataset": "/app/data/train.jsonl",
  "val_dataset": "/app/data/val.jsonl",  // Optional
  "model_path": "Qwen/Qwen2.5-0.5B-Instruct",
  "learning_rate": 0.00001,
  "batch_size": 4,
  "n_rollouts": 16,
  "num_steps": 1000,
  "max_prompt_length": 512,
  "max_response_length": 512,
  "save_interval": 100,
  "eval_interval": 50
}
```

**Response:**
```json
{
  "success": true,
  "job_id": "verl_20260309_142105_fe53b288",
  "status": "running",
  "process_id": 12345
}
```

**Example (PowerShell):**
```powershell
$request = @{
    train_dataset = "/app/data/my_dataset.jsonl"
    model_path = "Qwen/Qwen2.5-0.5B-Instruct"
    learning_rate = 1e-5
    batch_size = 4
    num_steps = 500
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:8090/verl/train" `
    -Method Post -Body $request -ContentType "application/json"
```

**Example (curl):**
```bash
curl -X POST http://localhost:8090/verl/train \
  -H "Content-Type: application/json" \
  -d '{
    "train_dataset": "/app/data/my_dataset.jsonl",
    "model_path": "Qwen/Qwen2.5-0.5B-Instruct",
    "learning_rate": 0.00001,
    "batch_size": 4,
    "num_steps": 500
  }'
```

---

#### 2. **List Training Jobs**

**GET** `/verl/jobs?limit={limit}&status={status}`

List training jobs with optional filtering.

**Query Parameters:**
- `limit` (optional): Maximum number of jobs to return (default: 10)
- `status` (optional): Filter by status (`pending`, `running`, `completed`, `failed`, `stopped`)

**Response:**
```json
{
  "success": true,
  "jobs": [
    {
      "_id": "verl_20260309_142105_fe53b288",
      "job_id": "verl_20260309_142105_fe53b288",
      "status": "running",
      "config": {
        "train_dataset": "/app/data/train.jsonl",
        "model_path": "Qwen/Qwen2.5-0.5B-Instruct",
        ...
      },
      "process_id": 12345,
      "created_at": "2026-03-09T14:21:05.123456",
      "started_at": "2026-03-09T14:21:10.654321",
      "metrics": {
        "current_step": 150,
        "total_steps": 1000
      }
    }
  ],
  "count": 1
}
```

**Examples:**
```powershell
# List all jobs
Invoke-RestMethod -Uri "http://localhost:8090/verl/jobs?limit=20" -Method Get

# List only running jobs
Invoke-RestMethod -Uri "http://localhost:8090/verl/jobs?status=running" -Method Get

# List completed jobs
Invoke-RestMethod -Uri "http://localhost:8090/verl/jobs?status=completed&limit=5" -Method Get
```

---

#### 3. **Get Job Status**

**GET** `/verl/jobs/{job_id}`

Get detailed status and metrics for a specific job.

**Response:**
```json
{
  "success": true,
  "job": {
    "_id": "verl_20260309_142105_fe53b288",
    "status": "running",
    "config": {...},
    "process_id": 12345,
    "log_file": "/app/verl_logs/verl_20260309_142105_fe53b288.log",
    "checkpoint_dir": "/app/verl_checkpoints/verl_20260309_142105_fe53b288/",
    "created_at": "2026-03-09T14:21:05.123456",
    "started_at": "2026-03-09T14:21:10.654321",
    "metrics": {
      "current_step": 150,
      "total_steps": 1000,
      "loss": 2.34,
      "reward": 0.78
    }
  }
}
```

**Example:**
```powershell
$jobId = "verl_20260309_142105_fe53b288"
$status = Invoke-RestMethod -Uri "http://localhost:8090/verl/jobs/$jobId" -Method Get

Write-Host "Status: $($status.job.status)"
Write-Host "Progress: $($status.job.metrics.current_step) / $($status.job.metrics.total_steps)"
```

---

#### 4. **Stop Training Job**

**DELETE** `/verl/jobs/{job_id}`

Stop a running training job gracefully (SIGTERM, then SIGKILL after 10s).

**Response:**
```json
{
  "success": true,
  "job_id": "verl_20260309_142105_fe53b288",
  "status": "stopped"
}
```

**Example:**
```powershell
$jobId = "verl_20260309_142105_fe53b288"
Invoke-RestMethod -Uri "http://localhost:8090/verl/jobs/$jobId" -Method Delete
```

---

#### 5. **Get Training Logs**

**GET** `/verl/jobs/{job_id}/logs?lines={lines}`

Retrieve training logs for a job.

**Query Parameters:**
- `lines` (optional): Number of log lines to return from the end (default: 100)

**Response:**
```json
{
  "success": true,
  "job_id": "verl_20260309_142105_fe53b288",
  "log_file": "/app/verl_logs/verl_20260309_142105_fe53b288.log",
  "lines": 100,
  "logs": "2026-03-09 14:21:10 - Starting PPO training...\n2026-03-09 14:21:15 - Step 1/1000, Loss: 3.45\n..."
}
```

**Example:**
```powershell
# Get last 50 lines
$jobId = "verl_20260309_142105_fe53b288"
$logs = Invoke-RestMethod -Uri "http://localhost:8090/verl/jobs/$jobId/logs?lines=50" -Method Get

Write-Host $logs.logs
```

---

## Configuration Guide

### Dataset Format (JSONL)

Each line must be valid JSON with `prompt` and `response` fields:

```json
{"prompt": "User question or instruction", "response": "Expected model output"}
{"prompt": "Another example prompt", "response": "Another response"}
```

**Requirements:**
- UTF-8 encoding
- One JSON object per line
- No trailing commas
- Valid JSON syntax

**Example Dataset Creation:**
```powershell
@'
{"prompt": "What is the capital of France?", "response": "The capital of France is Paris."}
{"prompt": "Explain quantum computing.", "response": "Quantum computing uses quantum-mechanical phenomena like superposition and entanglement to perform computation."}
{"prompt": "What is photosynthesis?", "response": "Photosynthesis is the process by which plants convert light energy into chemical energy."}
'@ | Out-File -FilePath dataset.jsonl -Encoding utf8
```

---

### Training Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `train_dataset` | string | **Required** | Path to training data (JSONL) |
| `val_dataset` | string | null | Path to validation data (JSONL) |
| `model_path` | string | "Qwen/Qwen2.5-0.5B-Instruct" | HuggingFace model path |
| `learning_rate` | float | 0.00001 | PPO learning rate |
| `batch_size` | int | 4 | Training batch size |
| `n_rollouts` | int | 16 | Rollouts per prompt |
| `num_steps` | int | 1000 | Total training steps |
| `max_prompt_length` | int | 512 | Max prompt tokens |
| `max_response_length` | int | 512 | Max response tokens |
| `save_interval` | int | 100 | Checkpoint frequency |
| `eval_interval` | int | 50 | Evaluation frequency |
| `kl_coef` | float | 0.05 | KL divergence coefficient |
| `value_loss_coef` | float | 0.5 | Value loss coefficient |
| `entropy_coef` | float | 0.01 | Entropy bonus coefficient |

---

### Supported Models

**Qwen Family (Recommended):**
- `Qwen/Qwen2.5-0.5B-Instruct` (fastest)
- `Qwen/Qwen2.5-1.5B-Instruct`
- `Qwen/Qwen2.5-3B-Instruct`
- `Qwen/Qwen2.5-7B-Instruct`

**Llama Family:**
- `meta-llama/Llama-3.2-1B-Instruct`
- `meta-llama/Llama-3.2-3B-Instruct`

**Mistral Family:**
- `mistralai/Mistral-7B-Instruct-v0.3`

---

## Workflow Examples

### Example 1: Simple Fine-Tuning (100 steps)

```powershell
# Create dataset
$dataset = @"
{"prompt": "Translate to French: Hello", "response": "Bonjour"}
{"prompt": "Translate to French: Goodbye", "response": "Au revoir"}
{"prompt": "Translate to French: Thank you", "response": "Merci"}
"@ | Out-File dataset.jsonl -Encoding utf8

# Copy to container
docker cp dataset.jsonl research-lightning-server:/app/dataset.jsonl

# Submit job
$job = Invoke-RestMethod -Uri "http://localhost:8090/verl/train" -Method Post `
    -Body (@{
        train_dataset = "/app/dataset.jsonl"
        model_path = "Qwen/Qwen2.5-0.5B-Instruct"
        learning_rate = 1e-5
        batch_size = 2
        n_rollouts = 8
        num_steps = 100
    } | ConvertTo-Json) `
    -ContentType "application/json"

Write-Host "Job started: $($job.job_id)"
```

---

### Example 2: Long Training with Validation

```powershell
# Submit job with validation dataset
$job = Invoke-RestMethod -Uri "http://localhost:8090/verl/train" -Method Post `
    -Body (@{
        project_name = "long_training_v1"
        train_dataset = "/app/data/train_large.jsonl"
        val_dataset = "/app/data/val.jsonl"
        model_path = "Qwen/Qwen2.5-1.5B-Instruct"
        learning_rate = 5e-6
        batch_size = 8
        n_rollouts = 32
        num_steps = 5000
        save_interval = 500
        eval_interval = 100
    } | ConvertTo-Json) `
    -ContentType "application/json"

$jobId = $job.job_id

# Monitor every 30 seconds
while ($true) {
    $status = Invoke-RestMethod -Uri "http://localhost:8090/verl/jobs/$jobId" -Method Get
    
    $progress = [math]::Round(($status.job.metrics.current_step / $status.job.metrics.total_steps) * 100, 1)
    
    Write-Host "[$($status.job.status)] Progress: $progress% ($($status.job.metrics.current_step)/$($status.job.metrics.total_steps))"
    
    if ($status.job.status -in @("completed", "failed", "stopped")) {
        break
    }
    
    Start-Sleep -Seconds 30
}
```

---

### Example 3: Batch Processing Multiple Datasets

```powershell
$datasets = @(
    "/app/data/dataset1.jsonl",
    "/app/data/dataset2.jsonl",
    "/app/data/dataset3.jsonl"
)

$jobs = @()

foreach ($dataset in $datasets) {
    $job = Invoke-RestMethod -Uri "http://localhost:8090/verl/train" -Method Post `
        -Body (@{
            train_dataset = $dataset
            model_path = "Qwen/Qwen2.5-0.5B-Instruct"
            num_steps = 500
        } | ConvertTo-Json) `
        -ContentType "application/json"
    
    $jobs += $job.job_id
    Write-Host "Started job: $($job.job_id) for $dataset"
    
    Start-Sleep -Seconds 5  # Stagger job submissions
}

# Monitor all jobs
Write-Host "`nMonitoring $($jobs.Count) jobs..."
Invoke-RestMethod -Uri "http://localhost:8090/verl/jobs?limit=20" -Method Get |
    Select-Object -ExpandProperty jobs |
    Where-Object { $_.job_id -in $jobs } |
    Format-Table job_id, status, @{L="Progress";E={
        "$($_.metrics.current_step)/$($_.metrics.total_steps)"
    }}
```

---

## Monitoring & Troubleshooting

### Real-Time Monitoring

```powershell
function Monitor-VERLJob {
    param(
        [string]$JobId,
        [int]$IntervalSeconds = 10
    )
    
    while ($true) {
        $status = Invoke-RestMethod -Uri "http://localhost:8090/verl/jobs/$JobId" -Method Get
        
        Clear-Host
        Write-Host "═══════════════════════════════════════" -ForegroundColor Cyan
        Write-Host "  VERL Job Monitor" -ForegroundColor Cyan
        Write-Host "═══════════════════════════════════════" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Job ID: $JobId"
        Write-Host "Status: $($status.job.status)" -ForegroundColor $(
            switch ($status.job.status) {
                "pending" { "Yellow" }
                "running" { "Cyan" }
                "completed" { "Green" }
                "failed" { "Red" }
                "stopped" { "Gray" }
            }
        )
        Write-Host ""
        
        if ($status.job.metrics) {
            $progress = [math]::Round(
                ($status.job.metrics.current_step / $status.job.metrics.total_steps) * 100, 1
            )
            Write-Host "Progress: $progress%"
            Write-Host "Steps: $($status.job.metrics.current_step) / $($status.job.metrics.total_steps)"
            
            if ($status.job.metrics.loss) {
                Write-Host "Loss: $($status.job.metrics.loss)"
            }
            if ($status.job.metrics.reward) {
                Write-Host "Reward: $($status.job.metrics.reward)"
            }
        }
        
        Write-Host ""
        Write-Host "Started: $($status.job.started_at)"
        
        if ($status.job.status -in @("completed", "failed", "stopped")) {
            Write-Host "Completed: $($status.job.completed_at)"
            break
        }
        
        Write-Host ""
        Write-Host "Refreshing in $IntervalSeconds seconds... (Ctrl+C to stop)"
        Start-Sleep -Seconds $IntervalSeconds
    }
}

# Usage
Monitor-VERLJob -JobId "verl_20260309_142105_fe53b288" -IntervalSeconds 15
```

---

### Common Issues

#### Issue: "MongoDB not available"

**Solution:**
```powershell
# Check MongoDB health
Invoke-RestMethod -Uri "http://localhost:8090/health" -Method Get |
    Select-Object -ExpandProperty storage |
    Format-List

# Verify MongoDB containers
docker ps | Select-String "mongo"

# Check MongoDB connectivity
docker exec mongo-primary mongosh --eval "db.adminCommand('ping')" -u lightning -p lightningpass --authenticationDatabase admin
```

---

#### Issue: "Dataset file not found"

**Solution:**
```powershell
# Verify file exists in container
docker exec research-lightning-server ls -la /app/*.jsonl

# Copy dataset again
docker cp your_dataset.jsonl research-lightning-server:/app/your_dataset.jsonl

# Use absolute path in API request
$body = @{ train_dataset = "/app/your_dataset.jsonl"; ... }
```

---

#### Issue: Job stuck in "pending" status

**Solution:**
```powershell
# Check server logs
docker logs research-lightning-server --tail 100

# Check job logs
$jobId = "your_job_id"
docker exec research-lightning-server cat /app/verl_logs/$jobId.log

# Restart job if needed
Invoke-RestMethod -Uri "http://localhost:8090/verl/jobs/$jobId" -Method Delete
# Then resubmit
```

---

## Best Practices

### 1. Dataset Quality
- **Size:** Minimum 50-100 examples for meaningful training
- **Diversity:** Varied prompts covering different use cases
- **Quality:** Well-formatted, grammatically correct responses
- **Balance:** Similar distribution of prompt types

### 2. Hyperparameter Tuning
- **Learning Rate:** Start with `1e-5`, adjust based on loss curves
- **Batch Size:** Use 4-8 for small models, 16-32 for larger models
- **Rollouts:** More rollouts = better exploration (16-32 typical)
- **Steps:** 500-1000 for fine-tuning, 5000+ for significant changes

### 3. Model Selection
- **Quick Experiments:** Qwen2.5-0.5B (fastest)
- **Production:** Qwen2.5-1.5B or 3B (best quality/speed balance)
- **High Quality:** Qwen2.5-7B or Llama-3.2-3B

### 4. Checkpoint Management
- Save checkpoints every 100-500 steps
- Keep last 3-5 checkpoints
- Test checkpoints before discarding
- Export best checkpoint for deployment

### 5. Monitoring
- Check progress every 5-10 minutes
- Monitor loss trends (should decrease)
- Watch for plateau (may need hyperparameter adjustment)
- Review logs for errors

---

## Advanced Usage

### Custom Hydra Configuration

For advanced users who need full control over VERL configuration:

```powershell
# The system automatically generates Hydra configs, but you can customize
# by modifying /app/verl-config.yaml in the container

docker exec research-lightning-server cat /app/verl-config.yaml

# Or provide custom values in the API request (they override defaults)
$customConfig = @{
    train_dataset = "/app/data/train.jsonl"
    model_path = "Qwen/Qwen2.5-1.5B-Instruct"
    learning_rate = 3e-6
    batch_size = 8
    n_rollouts = 32
    num_steps = 2000
    kl_coef = 0.1
    value_loss_coef = 0.8
    entropy_coef = 0.02
    max_prompt_length = 1024
    max_response_length = 1024
}
```

---

## Support & Resources

### Documentation
- **Validation Report:** `VERL_VALIDATION_COMPLETE.md`
- **Implementation Guide:** `VERL_JOB_MANAGEMENT_COMPLETE.md`
- **Quick Reference:** `VERL_VALIDATION_SUCCESS.md`

### Testing
- **Unit Tests:** `test-verl-job-management.ps1`
- **E2E Tests:** `test-verl-e2e-training.ps1`
- **Config Tests:** `test-hydra-config.ps1`

### Health Checks
```powershell
# Server health
Invoke-RestMethod -Uri "http://localhost:8090/health" -Method Get

# All jobs summary
Invoke-RestMethod -Uri "http://localhost:8090/verl/jobs?limit=50" -Method Get |
    Select-Object -ExpandProperty jobs |
    Group-Object status |
    Format-Table Count, Name
```

---

**Last Updated:** 2026-03-09  
**Version:** 1.0  
**Tested:** 8/8 validation tests passing ✅
