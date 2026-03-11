# APO (Agent Prompt Optimization) User Guide

## 📋 Table of Contents
1. [Overview](#overview)
2. [Quick Start](#quick-start)
3. [API Reference](#api-reference)
4. [Optimization Workflow](#optimization-workflow)
5. [Configuration](#configuration)
6. [Examples](#examples)
7. [Monitoring & Troubleshooting](#monitoring--troubleshooting)
8. [Best Practices](#best-practices)

---

## Overview

The **Agent Prompt Optimization (APO)** system provides automated optimization and evaluation of LLM prompts. It uses iterative refinement to improve prompt quality based on multiple evaluation criteria.

### Features
- ✅ **Iterative Optimization**: Automatically refine prompts across multiple iterations
- ✅ **Multi-Criteria Evaluation**: Assess prompts on coherence, relevance, and helpfulness
- ✅ **Version Control**: Track all prompt versions with performance metrics
- ✅ **MongoDB Persistence**: Durable storage for prompts and optimization runs
- ✅ **Concurrent Runs**: Support multiple simultaneous optimizations
- ✅ **Domain Organization**: Categorize prompts by domain/category
- ✅ **RESTful API**: Complete HTTP API for all operations

### Architecture
```
User Request → FastAPI Endpoint → APOManager
                                     ↓
                          ┌──────────┴──────────┐
                          ↓                     ↓
                   Optimization Loop      MongoDB Storage
                   (Background Task)      (Persistence)
                          ↓                     ↓
                   Evaluation Engine     Version Control
                   (Heuristic/LLM)       (Prompts + Runs)
```

---

## Quick Start

### 1. Verify APO is Enabled
```powershell
# Check server health
$response = Invoke-RestMethod -Uri "http://localhost:8090/health" -Method Get
$response.storage.type  # Should be "mongodb"
```

### 2. Start Your First Optimization
```powershell
$body = @{
    prompt_name = "my-assistant"
    initial_prompt = "You are a helpful AI assistant."
    domain = "general"
    description = "General purpose assistant prompt"
    iterations = 5
    evaluation_samples = 10
} | ConvertTo-Json

$response = Invoke-RestMethod `
    -Uri "http://localhost:8090/apo/optimize" `
    -Method Post `
    -Body $body `
    -ContentType "application/json"

Write-Host "Run ID: $($response.run_id)"
Write-Host "Prompt ID: $($response.prompt_id)"
```

### 3. Check Optimization Progress
```powershell
$runId = $response.run_id
$status = Invoke-RestMethod `
    -Uri "http://localhost:8090/apo/runs/$runId" `
    -Method Get

Write-Host "Status: $($status.status)"
Write-Host "Best Score: $($status.best_score)"
Write-Host "Improvement: $([math]::Round($status.improvement * 100, 2))%"
```

### 4. Retrieve Optimized Prompt
```powershell
$promptId = $response.prompt_id
$prompt = Invoke-RestMethod `
    -Uri "http://localhost:8090/apo/prompts/$promptId" `
    -Method Get

# Get the best version
$bestVersion = $prompt.versions | Sort-Object -Property performance_score -Descending | Select-Object -First 1
Write-Host "Best Prompt:"
Write-Host $bestVersion.template
```

---

## API Reference

### 🔹 POST /apo/optimize
Start a new prompt optimization run.

**Request Body:**
```json
{
  "prompt_name": "unique-prompt-name",
  "initial_prompt": "Your initial prompt template",
  "domain": "general",
  "description": "Optional description",
  "iterations": 5,
  "evaluation_samples": 10,
  "model": "Qwen/Qwen3.5-2B-Instruct",
  "optimization_strategy": "iterative_refinement",
  "evaluation_criteria": ["coherence", "relevance", "helpfulness"]
}
```

**Parameters:**
- `prompt_name` (string, **required**): Unique identifier for the prompt
- `initial_prompt` (string, **required**): Starting prompt template
- `domain` (string, default: "general"): Category (e.g., "coding", "medical", "general")
- `description` (string, optional): Human-readable description
- `iterations` (int, default: 5): Number of optimization iterations
- `evaluation_samples` (int, default: 10): Samples per evaluation
- `model` (string, default: "Qwen/Qwen3.5-2B-Instruct"): Model to use
- `optimization_strategy` (string, default: "iterative_refinement"): Strategy
- `evaluation_criteria` (array, default: ["coherence", "relevance", "helpfulness"]): Criteria

**Response:**
```json
{
  "message": "Optimization started",
  "run_id": "run_20260309_123456_abc123",
  "prompt_id": "prompt_20260309_123456_def456",
  "iterations": 5,
  "model": "Qwen/Qwen3.5-2B-Instruct",
  "status_endpoint": "/apo/runs/run_20260309_123456_abc123"
}
```

**PowerShell Example:**
```powershell
$body = @{
    prompt_name = "code-reviewer"
    initial_prompt = "Review this code and provide feedback."
    domain = "coding"
    iterations = 7
    evaluation_samples = 15
} | ConvertTo-Json

Invoke-RestMethod `
    -Uri "http://localhost:8090/apo/optimize" `
    -Method Post `
    -Body $body `
    -ContentType "application/json"
```

**cURL Example:**
```bash
curl -X POST http://localhost:8090/apo/optimize \
  -H "Content-Type: application/json" \
  -d '{
    "prompt_name": "code-reviewer",
    "initial_prompt": "Review this code and provide feedback.",
    "domain": "coding",
    "iterations": 7
  }'
```

---

### 🔹 GET /apo/runs/{run_id}
Get detailed status of an optimization run.

**Parameters:**
- `run_id` (path, **required**): Run identifier from POST /apo/optimize

**Response:**
```json
{
  "_id": "run_20260309_123456_abc123",
  "prompt_id": "prompt_20260309_123456_def456",
  "prompt_name": "code-reviewer",
  "status": "completed",
  "config": {
    "iterations": 7,
    "evaluation_samples": 15,
    "model": "Qwen/Qwen3.5-2B-Instruct",
    "optimization_strategy": "iterative_refinement",
    "criteria": ["coherence", "relevance", "helpfulness"]
  },
  "iterations_completed": [
    {
      "iteration": 1,
      "prompt_version": 1,
      "prompt": "Review this code and provide feedback.",
      "score": 0.75,
      "metrics": {
        "coherence": 0.6,
        "relevance": 0.8,
        "helpfulness": 0.85
      },
      "feedback": "Baseline evaluation",
      "duration_seconds": 0.12
    },
    // ... more iterations
  ],
  "best_version": 5,
  "best_score": 0.89,
  "improvement": 0.14,
  "created_at": "2026-03-09T12:34:56",
  "started_at": "2026-03-09T12:34:57",
  "completed_at": "2026-03-09T12:35:02",
  "total_duration_seconds": 5.2
}
```

**PowerShell Example:**
```powershell
$runId = "run_20260309_123456_abc123"
$status = Invoke-RestMethod `
    -Uri "http://localhost:8090/apo/runs/$runId" `
    -Method Get

# Display summary
Write-Host "Status: $($status.status)"
Write-Host "Iterations: $($status.iterations_completed.Count)"
Write-Host "Best Score: $($status.best_score)"
Write-Host "Improvement: $([math]::Round($status.improvement * 100, 2))%"
Write-Host "Duration: $($status.total_duration_seconds)s"
```

---

### 🔹 GET /apo/runs
List optimization runs with optional filtering.

**Query Parameters:**
- `limit` (int, default: 10): Maximum results to return
- `status` (string, optional): Filter by status ("pending", "running", "completed", "failed")

**Response:**
```json
{
  "runs": [
    {
      "_id": "run_20260309_123456_abc123",
      "prompt_name": "code-reviewer",
      "status": "completed",
      "best_score": 0.89,
      "improvement": 0.14,
      "created_at": "2026-03-09T12:34:56"
    },
    // ... more runs
  ],
  "count": 25,
  "limit": 10,
  "filter": {"status": "completed"}
}
```

**PowerShell Examples:**
```powershell
# List all runs (default limit: 10)
Invoke-RestMethod -Uri "http://localhost:8090/apo/runs" -Method Get

# List completed runs
Invoke-RestMethod -Uri "http://localhost:8090/apo/runs?status=completed" -Method Get

# List recent 50 runs
Invoke-RestMethod -Uri "http://localhost:8090/apo/runs?limit=50" -Method Get
```

---

### 🔹 GET /apo/prompts
List optimized prompts with optional filtering.

**Query Parameters:**
- `limit` (int, default: 10): Maximum results to return
- `domain` (string, optional): Filter by domain

**Response:**
```json
{
  "prompts": [
    {
      "_id": "prompt_20260309_123456_def456",
      "name": "code-reviewer",
      "description": "Code review assistant",
      "domain": "coding",
      "current_version": 5,
      "optimization_runs": 7,
      "versions": [
        {
          "version": 1,
          "template": "Review this code...",
          "performance_score": 0.75,
          "created_at": "2026-03-09T12:34:57"
        },
        // ... more versions
      ],
      "created_at": "2026-03-09T12:34:56",
      "last_optimized": "2026-03-09T12:35:02"
    }
  ],
  "count": 15,
  "limit": 10,
  "filter": {"domain": "coding"}
}
```

**PowerShell Examples:**
```powershell
# List all prompts
Invoke-RestMethod -Uri "http://localhost:8090/apo/prompts" -Method Get

# List coding prompts
Invoke-RestMethod -Uri "http://localhost:8090/apo/prompts?domain=coding" -Method Get

# Get top 20 prompts
Invoke-RestMethod -Uri "http://localhost:8090/apo/prompts?limit=20" -Method Get
```

---

### 🔹 GET /apo/prompts/{prompt_id}
Get detailed information about a specific prompt.

**Parameters:**
- `prompt_id` (path, **required**): Prompt identifier

**Response:**
```json
{
  "_id": "prompt_20260309_123456_def456",
  "name": "code-reviewer",
  "description": "Code review assistant",
  "domain": "coding",
  "current_version": 5,
  "optimization_runs": 7,
  "versions": [
    {
      "version": 1,
      "template": "Review this code and provide feedback.",
      "performance_score": 0.75,
      "metrics": {
        "coherence": 0.6,
        "relevance": 0.8,
        "helpfulness": 0.85
      },
      "created_at": "2026-03-09T12:34:57"
    },
    {
      "version": 5,
      "template": "Review this code thoroughly. Provide specific feedback on:\n1. Code quality\n2. Best practices\n3. Potential bugs",
      "performance_score": 0.89,
      "metrics": {
        "coherence": 0.85,
        "relevance": 0.92,
        "helpfulness": 0.91
      },
      "created_at": "2026-03-09T12:35:02"
    }
  ],
  "created_at": "2026-03-09T12:34:56",
  "last_optimized": "2026-03-09T12:35:02"
}
```

**PowerShell Example:**
```powershell
$promptId = "prompt_20260309_123456_def456"
$prompt = Invoke-RestMethod `
    -Uri "http://localhost:8090/apo/prompts/$promptId" `
    -Method Get

# Get best performing version
$best = $prompt.versions | Sort-Object -Property performance_score -Descending | Select-Object -First 1

Write-Host "Best Prompt (Score: $($best.performance_score)):"
Write-Host $best.template
```

---

## Optimization Workflow

### How It Works

1. **Initialization**
   - User submits initial prompt via POST /apo/optimize
   - System creates prompt document and run document in MongoDB
   - Background optimization task launched asynchronously

2. **Iterative Refinement**
   - For each iteration (1 to N):
     - Evaluate current prompt version
     - Generate improved version based on evaluation
     - Calculate performance metrics
     - Save version to MongoDB
     - Check if target score reached

3. **Evaluation**
   - **Heuristic Mode** (current): Simple rule-based scoring
     - Coherence: Sentence structure and clarity
     - Relevance: Keyword matching and context
     - Helpfulness: Actionable guidance indicators
   - **LLM Mode** (future): Use LLM to evaluate prompt quality

4. **Completion**
   - Run status updated to "completed"
   - Best version identified and saved
   - Total improvement calculated
   - Results persisted in MongoDB

### Evaluation Criteria

**Coherence** (0.0 - 1.0)
- Sentence structure quality
- Logical flow
- Grammar and punctuation

**Relevance** (0.0 - 1.0)
- Domain-specific keywords
- Task alignment
- Context appropriateness

**Helpfulness** (0.0 - 1.0)
- Actionable guidance
- Clarity of instructions
- User-friendliness

### Optimization Strategies

**iterative_refinement** (Default)
- Start with initial prompt
- Evaluate and identify weaknesses
- Generate improved version
- Repeat for N iterations
- Select best performing version

---

## Configuration

### Environment Variables

Configure APO in `docker-compose.ai.yml`:

```yaml
environment:
  APO__ENABLED: "true"
  APO__DEFAULT_MODEL: "Qwen/Qwen3.5-2B-Instruct"
  APO__DEFAULT_ITERATIONS: "5"
  APO__OPTIMIZATION_STRATEGY: "iterative_refinement"
  APO__MONGODB_PROMPTS_COLLECTION: "apo_prompts"
  APO__MONGODB_RUNS_COLLECTION: "apo_optimization_runs"
```

### Configuration Fields

#### Core Settings
- `APO__ENABLED`: Enable/disable APO (default: false)
- `APO__DEFAULT_MODEL`: Default model for optimization
- `APO__DEFAULT_ITERATIONS`: Default iteration count
- `APO__OPTIMIZATION_STRATEGY`: Default optimization strategy
- `APO__MONGODB_PROMPTS_COLLECTION`: MongoDB collection for prompts
- `APO__MONGODB_RUNS_COLLECTION`: MongoDB collection for runs

#### LLM Evaluation Settings (NEW)
- `APO__EVALUATION_USE_LLM`: Use LLM for evaluation instead of heuristics (default: false)
- `APO__EVALUATION_LLM_ENDPOINT`: vLLM API endpoint (default: "http://localhost:8000")
- `APO__EVALUATION_LLM_MODEL`: Model name for evaluation (must match vLLM served model)
- `APO__EVALUATION_LLM_TIMEOUT`: HTTP timeout in seconds (default: 30)
- `APO__EVALUATION_FALLBACK_TO_HEURISTIC`: Auto-fallback on LLM errors (default: true)

### Evaluation Modes

APO supports two evaluation modes:

#### 1. **Heuristic Evaluation** (Fast, Baseline)
- **Speed**: ~0.1 seconds per iteration
- **Feedback**: ~36 characters (simple bullet points)
- **Reliability**: 100%
- **Use Case**: Quick testing, high-throughput optimization
- **Configuration**: `APO__EVALUATION_USE_LLM: "false"` (default)

**Example Feedback:**
```
Baseline evaluation using heuristics
```

#### 2. **LLM Evaluation** (Detailed, Context-Aware)
- **Speed**: ~2-5 seconds per iteration
- **Feedback**: 600-2,100 characters (detailed analysis with reasoning)
- **Reliability**: 98%+ (with automatic fallback)
- **Use Case**: Production optimization, quality-focused refinement
- **Configuration**: `APO__EVALUATION_USE_LLM: "true"`

**Example Feedback:**
```
- Coherence: 0.95
- Relevance: 0.92
- Helpfulness: 0.90
- overall_assessment: "The prompt has a clear structure with well-defined 
  sections, logical flow, and proper grammar. It is appropriate for its 
  intended use case and domain. The prompt provides clear guidance for 
  actionable responses."

<think>
Thinking Process:
1. Analyze the Request:
   * Input: A specific prompt template intended for an AI assistant.
   * Task: Evaluate the prompt template across multiple criteria...
   [detailed reasoning continues]
</think>
```

### LLM Evaluation Setup

**1. Verify vLLM is Running:**
```powershell
# Check vLLM endpoint
curl http://localhost:8001/v1/models

# Expected output: model ID (e.g., "Qwen/Qwen3.5-2B")
```

**2. Configure docker-compose.ai.yml:**
```yaml
environment:
  APO__ENABLED: "true"
  APO__EVALUATION_USE_LLM: "true"
  APO__EVALUATION_LLM_ENDPOINT: "http://host.docker.internal:8001"
  APO__EVALUATION_LLM_MODEL: "Qwen/Qwen3.5-2B"  # Must match vLLM model!
  APO__EVALUATION_LLM_TIMEOUT: "30"
  APO__EVALUATION_FALLBACK_TO_HEURISTIC: "true"
```

**3. Recreate Lightning Server:**
```powershell
cd Docker
docker-compose -f docker-compose.ai.yml up -d --force-recreate lightning-server
# Wait ~20 seconds for startup
```

**4. Verify LLM Evaluation:**
```powershell
# Run test suite
.\test-apo-llm-evaluation.ps1

# Or test manually
$body = @{
    prompt_name = "test-llm"
    initial_prompt = "You are a helpful assistant."
    iterations = 2
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "http://localhost:8090/apo/optimize" -Method Post -Body $body -ContentType "application/json"

# Wait for completion
Start-Sleep -Seconds 10

# Check feedback length (>500 chars = LLM, ~36 chars = heuristic)
$status = Invoke-RestMethod -Uri "http://localhost:8090/apo/runs/$($response.run_id)"
$status.iterations_completed[0].feedback.Length  # Should be 600-2,100 if LLM is working
```

### Evaluation Comparison Table

| Feature | Heuristic | LLM |
|---------|-----------|-----|
| **Speed** | 0.1s | 2.74s avg |
| **Feedback Length** | 36 chars | 600-2,100 chars |
| **Detail Level** | Simple rules | Detailed analysis |
| **Reasoning** | None | Included (thinking process) |
| **Context Awareness** | Basic | Advanced |
| **Reliability** | 100% | 98%+ (with fallback) |
| **Throughput** | ~600/min | ~22/min |
| **Best For** | Testing, prototyping | Production, quality |

### Troubleshooting LLM Evaluation

**Issue: Falling back to heuristic (feedback ~36 chars)**

Possible causes:
1. **Model name mismatch**
   ```powershell
   # Check vLLM model name
   curl http://localhost:8001/v1/models | jq '.data[0].id'

   # Update docker-compose.ai.yml to match exactly
   APO__EVALUATION_LLM_MODEL: "Qwen/Qwen3.5-2B"  # Example
   ```

2. **Network connectivity**
   ```powershell
   # Test from Lightning Server container
   docker exec research-lightning-server curl http://host.docker.internal:8001/v1/models

   # Should return HTTP 200
   ```

3. **vLLM not running**
   ```powershell
   # Check vLLM status
   docker ps | grep vllm

   # Start if needed
   cd Docker/lightning-server
   docker-compose up -d
   ```

4. **Check logs**
   ```powershell
   docker logs research-lightning-server --tail 50 | Select-String "LLM evaluation"

   # Look for error messages
   ```

---

## Examples

### Example 1: Optimize Assistant Prompt
```powershell
# Create optimization request
$request = @{
    prompt_name = "helpful-assistant"
    initial_prompt = "You are a helpful assistant."
    domain = "general"
    description = "General purpose helpful assistant"
    iterations = 5
    evaluation_samples = 10
    evaluation_criteria = @("coherence", "relevance", "helpfulness")
} | ConvertTo-Json

# Start optimization
$result = Invoke-RestMethod `
    -Uri "http://localhost:8090/apo/optimize" `
    -Method Post `
    -Body $request `
    -ContentType "application/json"

# Wait for completion (check every 2 seconds)
$runId = $result.run_id
do {
    Start-Sleep -Seconds 2
    $status = Invoke-RestMethod -Uri "http://localhost:8090/apo/runs/$runId" -Method Get
    Write-Host "Status: $($status.status) - Score: $($status.best_score)"
} while ($status.status -ne "completed" -and $status.status -ne "failed")

# Get optimized prompt
$promptId = $result.prompt_id
$prompt = Invoke-RestMethod -Uri "http://localhost:8090/apo/prompts/$promptId" -Method Get
$bestVersion = $prompt.versions | Sort-Object -Property performance_score -Descending | Select-Object -First 1

Write-Host "`nOptimized Prompt:"
Write-Host $bestVersion.template
Write-Host "`nScore: $($bestVersion.performance_score)"
```

### Example 2: Batch Optimization
```powershell
# Define multiple prompts to optimize
$prompts = @(
    @{
        prompt_name = "code-explainer"
        initial_prompt = "Explain this code."
        domain = "coding"
    },
    @{
        prompt_name = "bug-finder"
        initial_prompt = "Find bugs in this code."
        domain = "coding"
    },
    @{
        prompt_name = "doc-writer"
        initial_prompt = "Write documentation for this code."
        domain = "coding"
    }
)

# Start all optimizations
$runs = @()
foreach ($prompt in $prompts) {
    $body = $prompt | ConvertTo-Json
    $result = Invoke-RestMethod `
        -Uri "http://localhost:8090/apo/optimize" `
        -Method Post `
        -Body $body `
        -ContentType "application/json"
    $runs += $result.run_id
    Write-Host "Started: $($prompt.prompt_name) - Run ID: $($result.run_id)"
}

# Wait for all to complete
Write-Host "`nWaiting for all optimizations to complete..."
Start-Sleep -Seconds 10

# Check results
foreach ($runId in $runs) {
    $status = Invoke-RestMethod -Uri "http://localhost:8090/apo/runs/$runId" -Method Get
    Write-Host "$($status.prompt_name): Score $($status.best_score) (Improvement: $([math]::Round($status.improvement * 100, 2))%)"
}
```

### Example 3: Monitor Real-Time Progress
```powershell
function Monitor-OptimizationRun {
    param([string]$RunId)
    
    $lastIterations = 0
    
    while ($true) {
        $status = Invoke-RestMethod -Uri "http://localhost:8090/apo/runs/$RunId" -Method Get
        
        $currentIterations = $status.iterations_completed.Count
        
        if ($currentIterations -gt $lastIterations) {
            $latest = $status.iterations_completed[$currentIterations - 1]
            Write-Host "Iteration $($latest.iteration): Score $($latest.score)" -ForegroundColor Cyan
            Write-Host "  Coherence: $($latest.metrics.coherence)"
            Write-Host "  Relevance: $($latest.metrics.relevance)"
            Write-Host "  Helpfulness: $($latest.metrics.helpfulness)"
            $lastIterations = $currentIterations
        }
        
        if ($status.status -eq "completed" -or $status.status -eq "failed") {
            Write-Host "`nFinal Status: $($status.status)" -ForegroundColor Green
            Write-Host "Best Score: $($status.best_score)"
            Write-Host "Total Improvement: $([math]::Round($status.improvement * 100, 2))%"
            break
        }
        
        Start-Sleep -Seconds 1
    }
}

# Usage
$result = Invoke-RestMethod ... # Start optimization
Monitor-OptimizationRun -RunId $result.run_id
```

---

## Monitoring & Troubleshooting

### Check APO Status
```powershell
# Server health
$health = Invoke-RestMethod -Uri "http://localhost:8090/health" -Method Get
Write-Host "Storage: $($health.storage.type)"  # Should be "mongodb"

# List recent runs
$runs = Invoke-RestMethod -Uri "http://localhost:8090/apo/runs?limit=5" -Method Get
$runs.runs | ForEach-Object {
    Write-Host "$($_.prompt_name): $($_.status) - Score: $($_.best_score)"
}
```

### Common Issues

**Issue: APO endpoints return 503 "APO not enabled"**
- **Cause**: APO not enabled in configuration
- **Solution**: Set `APO__ENABLED: "true"` in docker-compose.ai.yml and restart

**Issue: Optimization runs fail immediately**
- **Cause**: MongoDB connection issue
- **Solution**: Check MongoDB is running and accessible
  ```powershell
  docker logs research-lightning-server | Select-String "MongoDB"
  ```

**Issue: Low optimization scores**
- **Cause**: Using heuristic evaluation mode (baseline)
- **Solution**: Enable LLM evaluation for more accurate, context-aware scoring:
  ```yaml
  # docker-compose.ai.yml
  APO__EVALUATION_USE_LLM: "true"
  APO__EVALUATION_LLM_ENDPOINT: "http://host.docker.internal:8001"
  APO__EVALUATION_LLM_MODEL: "Qwen/Qwen3.5-2B"
  ```
  Then recreate container: `docker-compose -f docker-compose.ai.yml up -d --force-recreate lightning-server`

**Issue: Slow optimization**
- **Cause**: High iteration count or evaluation samples
- **Solution**: Reduce iterations and evaluation_samples for faster results

### Logs
```powershell
# View APO logs
docker logs research-lightning-server --tail 100 | Select-String "APO"

# View real-time logs
docker logs -f research-lightning-server
```

### MongoDB Inspection
```powershell
# Connect to MongoDB container
docker exec -it mongo-primary mongosh -u lightning -p lightningpass --authenticationDatabase admin

# In MongoDB shell:
use lightning
db.apo_prompts.find().pretty()
db.apo_optimization_runs.find().pretty()
```

---

## Best Practices

### 1. Naming Conventions
- Use descriptive, unique prompt names
- Include domain prefix: "coding-reviewer", "medical-diagnosis"
- Avoid special characters

### 2. Iteration Count
- Start with 3-5 iterations for testing
- Use 5-10 for production optimization
- More iterations = diminishing returns

### 3. Domain Organization
- Categorize prompts by domain
- Use consistent domain names
- Makes filtering and management easier

### 4. Version Control
- Review all versions, not just the best
- Sometimes v2 is better than v5 for specific use cases
- Keep version history for rollback

### 5. Evaluation Strategy
- **Heuristic Mode**: Fast baseline scoring (~0.1s per iteration, 100% reliable)
  - Good for: Rapid testing, high-throughput optimization, prototyping
- **LLM Mode**: Detailed context-aware evaluation (~2.7s per iteration, 98%+ reliable)
  - Good for: Production optimization, quality-focused refinement, detailed feedback
  - Enable with: `APO__EVALUATION_USE_LLM: "true"`
- **Hybrid Approach**: Start with heuristic for quick iterations, then use LLM for final refinement
- **Custom Evaluation**: Consider domain-specific evaluation logic for specialized use cases

### 6. Performance
- Run optimizations during low-traffic periods
- Use concurrent runs for batch optimization
- Monitor MongoDB storage growth

### 7. Documentation
- Add descriptions to all prompts
- Document domain-specific criteria
- Track optimization patterns

---

## Next Steps

1. **Integrate LLM Evaluation**
   - Replace heuristic evaluation with LLM-based assessment
   - Use GPT-4/Claude for high-quality prompt analysis
   - Implement custom evaluation prompts

2. **Advanced Strategies**
   - Implement beam search optimization
   - Add genetic algorithm strategy
   - Support A/B testing of prompts

3. **Analytics Dashboard**
   - Visualize optimization trends
   - Compare prompts across domains
   - Track improvement over time

4. **Production Deployment**
   - Set up monitoring alerts
   - Implement rate limiting
   - Add authentication for API endpoints

---

## Support

For issues or questions:
- Check logs: `docker logs research-lightning-server`
- Review MongoDB data: `docker exec -it mongo-primary mongosh`
- Run test suite: `.\test-apo-optimization.ps1`

---

**APO Version**: 1.0.0  
**Last Updated**: 2026-03-09  
**Status**: ✅ Production Ready
