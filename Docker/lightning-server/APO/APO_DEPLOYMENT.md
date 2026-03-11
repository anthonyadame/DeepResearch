# APO (Automatic Prompt Optimization) Deployment Guide

**Lightning Server - APO Algorithm Integration**

## Table of Contents
1. [Overview](#overview)
2. [What is APO?](#what-is-apo)
3. [Architecture](#architecture)
4. [Configuration](#configuration)
5. [API Reference](#api-reference)
6. [Optimization Workflow](#optimization-workflow)
7. [Performance Tuning](#performance-tuning)
8. [Monitoring & Metrics](#monitoring--metrics)
9. [Best Practices](#best-practices)
10. [Troubleshooting](#troubleshooting)
11. [Examples](#examples)

---

## Overview

**APO (Automatic Prompt Optimization)** is a gradient-based prompt engineering technique that uses **beam search** and **LLM-as-optimizer** to automatically improve prompt templates for better task performance.

### Key Features

✅ **Beam Search Exploration** - Explores multiple prompt variations simultaneously  
✅ **Gradient-Based Refinement** - Uses LLM feedback to compute improvement directions  
✅ **Multi-Round Optimization** - Iteratively improves prompts over multiple rounds  
✅ **Validation-Driven** - Evaluates prompts on held-out test sets  
✅ **Lightning Store Integration** - Tracks optimization history and metrics  
✅ **LLM-Agnostic** - Works with vLLM, OpenAI, Anthropic, or any LLM backend  

### When to Use APO

**Use APO when:**
- 🎯 You have a well-defined task with clear success criteria
- 📊 You have representative validation examples (10-100+)
- 🔄 You want to automate prompt engineering instead of manual iteration
- 📈 You need reproducible, data-driven prompt improvements
- 💰 You want to reduce prompt engineering time and human effort

**Don't use APO when:**
- ❌ Task definition is ambiguous or changing frequently
- ❌ You have fewer than 5-10 validation examples
- ❌ Prompt quality doesn't significantly impact performance
- ❌ Manual prompt engineering is sufficient

---

## What is APO?

### The Problem: Manual Prompt Engineering is Hard

Traditional prompt engineering involves:
1. 🧪 **Trial and error** - Testing many prompt variations manually
2. 🤔 **Subjective judgment** - Hard to quantify what makes a "good" prompt
3. ⏱️ **Time-consuming** - Each iteration requires human effort
4. 📉 **Not reproducible** - Different engineers produce different results

### The Solution: Automatic Prompt Optimization

APO automates this process using:

1. **Beam Search** - Explores multiple prompt candidates in parallel
2. **LLM Gradients** - Uses LLM feedback to compute improvement directions
3. **Edit Application** - Automatically refines prompts based on gradients
4. **Objective Evaluation** - Scores prompts on validation examples

### How APO Works

```
┌─────────────────────────────────────────────────────────┐
│                  APO Optimization Loop                   │
└─────────────────────────────────────────────────────────┘

Round 1:
  Initial Prompt → [Beam Search] → Variation 1, 2, 3, ...
                                    ↓
                    [Compute Gradients via LLM Feedback]
                                    ↓
                    [Apply Edits to Improve Prompts]
                                    ↓
                    [Evaluate on Validation Set]
                                    ↓
                    Select Best Prompt → Score: 0.75

Round 2:
  Best Prompt → [Beam Search] → New Variations
                                ↓
                [Gradients → Edits → Evaluate]
                                ↓
                Select Best → Score: 0.82 ✨

Round N:
  Converged or Early Stopping → Final Optimized Prompt
```

### Key Concepts

#### 1. **Beam Width**
Number of prompt variations explored in each round.
- **Wider beam** (5-10) = more exploration, higher cost
- **Narrower beam** (2-4) = faster convergence, less diversity

#### 2. **Branch Factor**
How many new variations to generate from each beam candidate.
- Controls exploration-exploitation tradeoff

#### 3. **Gradient Model**
LLM used to analyze prompts and suggest improvements.
- Typically a strong reasoning model (GPT-4, Claude, Llama-3-70B)

#### 4. **Apply Edit Model**
LLM used to actually modify prompts based on gradients.
- Can be same as gradient model or a faster model

#### 5. **Diversity Temperature**
Controls variation diversity during beam search.
- **Higher temp** (0.8-1.2) = more diverse variations
- **Lower temp** (0.3-0.6) = more conservative changes

---

## Architecture

### Component Diagram

```
┌──────────────────────────────────────────────────────────┐
│                   Lightning Server                        │
│                                                           │
│  ┌────────────────────────────────────────────────────┐  │
│  │          APO Optimization Manager                   │  │
│  │                                                     │  │
│  │  • initialize_optimizer()                          │  │
│  │  • start_optimization()                            │  │
│  │  • get_optimization_status()                       │  │
│  │  • get_best_prompt()                               │  │
│  └──────────┬──────────────────────────┬──────────────┘  │
│             │                          │                  │
│             ▼                          ▼                  │
│  ┌──────────────────┐      ┌──────────────────────┐     │
│  │   LLM Client     │      │  Lightning Store     │     │
│  │  (vLLM/OpenAI)   │      │  (Optimization Log)  │     │
│  └──────────────────┘      └──────────────────────┘     │
└──────────────────────────────────────────────────────────┘
                    │
                    ▼
        ┌────────────────────────┐
        │  vLLM / LiteLLM Proxy  │
        │                        │
        │  • Gradient Model      │
        │  • Apply Edit Model    │
        └────────────────────────┘
```

### Data Flow

1. **User** → POST `/api/apo/initialize` → Initializes optimizer with task
2. **User** → POST `/api/apo/optimize` → Starts optimization loop
3. **APO Manager** → Generates variations via beam search
4. **APO Manager** → Calls LLM to compute gradients
5. **APO Manager** → Calls LLM to apply edits
6. **APO Manager** → Evaluates prompts on validation set
7. **APO Manager** → Logs results to Lightning Store
8. **APO Manager** → Returns best prompt to user

---

## Configuration

### Environment Variables

Add to `.env`:

```bash
# ========== APO Configuration ==========
APO_ENABLED=true
APO_GRADIENT_MODEL=gpt-4o-mini
APO_APPLY_EDIT_MODEL=gpt-4o-mini
APO_BEAM_WIDTH=4
APO_BRANCH_FACTOR=3
APO_BEAM_ROUNDS=5
APO_GRADIENT_BATCH_SIZE=8
APO_VAL_BATCH_SIZE=16
APO_DIVERSITY_TEMPERATURE=0.7

# LLM Backend (vLLM or OpenAI)
VLLM_API_BASE=http://vllm-server:8000/v1
VLLM_API_KEY=EMPTY

# Or use OpenAI
# OPENAI_API_BASE=https://api.openai.com/v1
# OPENAI_API_KEY=sk-...
```

### Configuration Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `APO_ENABLED` | `false` | Enable APO optimization features |
| `APO_GRADIENT_MODEL` | `gpt-4o-mini` | Model for computing gradients |
| `APO_APPLY_EDIT_MODEL` | `gpt-4o-mini` | Model for applying edits |
| `APO_BEAM_WIDTH` | `4` | Number of variations per round |
| `APO_BRANCH_FACTOR` | `3` | Variations per beam candidate |
| `APO_BEAM_ROUNDS` | `5` | Maximum optimization rounds |
| `APO_GRADIENT_BATCH_SIZE` | `8` | Batch size for gradient computation |
| `APO_VAL_BATCH_SIZE` | `16` | Batch size for validation |
| `APO_DIVERSITY_TEMPERATURE` | `0.7` | Temperature for variation generation |

### Model Selection Guidelines

#### Gradient Model (Analyzes Prompts)

**Best:** GPT-4, Claude Opus, Llama-3-70B
- Needs strong reasoning to identify improvement opportunities

**Good:** GPT-4o-mini, Claude Sonnet, Llama-3-8B
- Acceptable for simpler tasks

**Avoid:** Small models (<7B parameters)
- May produce low-quality gradient feedback

#### Apply Edit Model (Modifies Prompts)

**Best:** Same as gradient model
- Most consistent results

**Good:** Faster model than gradient model
- Saves cost if gradient model is expensive (e.g., GPT-4)

**Avoid:** Models with different instruction formats
- Can cause format inconsistencies

---

## API Reference

### 1. Initialize Optimizer

Initialize APO optimizer with task definition and validation examples.

**Endpoint:** `POST /api/apo/initialize`

**Request:**
```json
{
  "initialPrompt": "You are a helpful assistant that...",
  "taskDescription": "Summarize customer support tickets in 2-3 sentences",
  "validationExamples": [
    {
      "input": "Customer complains about slow shipping...",
      "expected_output": "Customer reports delayed delivery for order #12345.",
      "metadata": {"difficulty": "easy"}
    },
    {
      "input": "User has trouble logging in after password reset...",
      "expected_output": "Login issue post-password-reset, requires account verification.",
      "metadata": {"difficulty": "medium"}
    }
  ]
}
```

**Response:**
```json
{
  "success": true,
  "initialPrompt": "You are a helpful assistant that...",
  "taskDescription": "Summarize customer support tickets in 2-3 sentences",
  "validationExamples": 2,
  "message": "APO optimizer initialized successfully"
}
```

**Error Responses:**
- `400 Bad Request` - Missing `initialPrompt` or `taskDescription`
- `503 Service Unavailable` - APO not enabled or manager not available

---

### 2. Start Optimization

Start the beam search optimization process.

**Endpoint:** `POST /api/apo/optimize`

**Request:**
```json
{
  "numRounds": 5,
  "earlyStoppingThreshold": 0.01
}
```

**Parameters:**
- `numRounds` (optional): Number of optimization rounds (default: from config)
- `earlyStoppingThreshold` (optional): Stop if improvement < threshold (default: 0.01)

**Response:**
```json
{
  "success": true,
  "run_id": "apo_run_20240115_143022",
  "rounds_completed": 5,
  "best_score": 0.87,
  "best_prompt": "You are an expert customer support analyst...",
  "started_at": "2024-01-15T14:30:22Z",
  "completed_at": "2024-01-15T14:35:47Z"
}
```

**Error Responses:**
- `503 Service Unavailable` - APO not enabled, manager not available, or optimizer not initialized

---

### 3. Get Optimization Status

Get current optimization status and history.

**Endpoint:** `GET /api/apo/status`

**Response:**
```json
{
  "active": false,
  "best_prompt": "You are an expert customer support analyst...",
  "best_score": 0.87,
  "rounds_completed": 5,
  "history": [
    {
      "round": 5,
      "prompt": "You are an expert...",
      "score": 0.87,
      "beam_scores": [0.87, 0.84, 0.82, 0.79],
      "timestamp": "2024-01-15T14:35:47Z"
    },
    {
      "round": 4,
      "prompt": "You are a skilled...",
      "score": 0.84,
      "beam_scores": [0.84, 0.81, 0.78, 0.75],
      "timestamp": "2024-01-15T14:34:12Z"
    }
  ],
  "config": {
    "beam_width": 4,
    "beam_rounds": 5,
    "branch_factor": 3,
    "gradient_model": "gpt-4o-mini"
  }
}
```

---

### 4. Get Best Prompt

Get the current best optimized prompt.

**Endpoint:** `GET /api/apo/best-prompt`

**Response:**
```json
{
  "success": true,
  "prompt": "You are an expert customer support analyst...",
  "score": 0.87,
  "timestamp": "2024-01-15T14:35:47Z"
}
```

**Error Responses:**
- `404 Not Found` - No optimization has been run yet

---

## Optimization Workflow

### Step-by-Step Guide

#### 1. **Prepare Validation Examples**

Create 10-100+ examples representing your task:

```python
validation_examples = [
    {
        "input": "Customer says: I want a refund for my order",
        "expected_output": "Refund request for order",
        "metadata": {"category": "refund"}
    },
    {
        "input": "User reports: App crashes on login",
        "expected_output": "App crash bug during authentication",
        "metadata": {"category": "bug"}
    },
    # ... more examples
]
```

**Quality over quantity:**
- ✅ Cover diverse scenarios (easy, medium, hard)
- ✅ Include edge cases
- ✅ Use real-world examples when possible
- ❌ Avoid duplicate or trivial examples

#### 2. **Initialize Optimizer**

```bash
curl -X POST http://localhost:9090/api/apo/initialize \
  -H "Content-Type: application/json" \
  -d '{
    "initialPrompt": "You are a helpful assistant that summarizes customer support tickets.",
    "taskDescription": "Summarize tickets in 2-3 sentences focusing on the core issue.",
    "validationExamples": [...]
  }'
```

#### 3. **Start Optimization**

```bash
curl -X POST http://localhost:9090/api/apo/optimize \
  -H "Content-Type: application/json" \
  -d '{
    "numRounds": 5,
    "earlyStoppingThreshold": 0.01
  }'
```

**This will:**
1. Run 5 rounds of beam search
2. Generate 4 variations per round (beam width)
3. Compute gradients for each variation
4. Apply edits to improve prompts
5. Evaluate on validation examples
6. Track best prompt and score

#### 4. **Monitor Progress**

```bash
# Check status during optimization
curl http://localhost:9090/api/apo/status

# Get best prompt so far
curl http://localhost:9090/api/apo/best-prompt
```

#### 5. **Use Optimized Prompt**

Once optimization completes:

```python
import requests

response = requests.get("http://localhost:9090/api/apo/best-prompt")
best_prompt = response.json()["prompt"]

# Use in your application
completion = llm.chat.completions.create(
    model="gpt-4o-mini",
    messages=[
        {"role": "system", "content": best_prompt},
        {"role": "user", "content": "Customer complains about..."}
    ]
)
```

---

## Performance Tuning

### Optimization Speed vs. Quality

#### Fast Optimization (Cost-Optimized)
```bash
APO_BEAM_WIDTH=2
APO_BRANCH_FACTOR=2
APO_BEAM_ROUNDS=3
APO_GRADIENT_MODEL=gpt-4o-mini
```
- **Time:** ~2-5 minutes
- **Cost:** Low ($0.10-$0.50)
- **Use when:** Rapid iteration, simple tasks

#### Balanced Optimization (Recommended)
```bash
APO_BEAM_WIDTH=4
APO_BRANCH_FACTOR=3
APO_BEAM_ROUNDS=5
APO_GRADIENT_MODEL=gpt-4o-mini
```
- **Time:** ~5-15 minutes
- **Cost:** Medium ($0.50-$2.00)
- **Use when:** Most production use cases

#### High-Quality Optimization (Research)
```bash
APO_BEAM_WIDTH=8
APO_BRANCH_FACTOR=4
APO_BEAM_ROUNDS=10
APO_GRADIENT_MODEL=gpt-4
```
- **Time:** ~30-60 minutes
- **Cost:** High ($5-$20)
- **Use when:** Critical prompts, research experiments

### Cost Estimation

**Formula:**
```
Total LLM Calls = BEAM_WIDTH × BRANCH_FACTOR × BEAM_ROUNDS × 3
                   (variations)  (new per beam)   (rounds)     (gradient + edit + eval)
```

**Example (Balanced):**
```
Calls = 4 × 3 × 5 × 3 = 180 LLM API calls
Cost (GPT-4o-mini @ $0.15/1M tokens):
  - Input: ~500 tokens/call × 180 = 90K tokens = $0.01
  - Output: ~200 tokens/call × 180 = 36K tokens = $0.01
  Total: ~$0.02 per optimization run
```

### Parallelization

APO can parallelize LLM calls within each round:

```python
# config.py
APO_GRADIENT_BATCH_SIZE=8  # Process 8 variations concurrently
APO_VAL_BATCH_SIZE=16      # Evaluate 16 examples concurrently
```

**Trade-offs:**
- ✅ Faster optimization (2-4x speedup)
- ⚠️ Higher memory usage
- ⚠️ Rate limits may apply (especially OpenAI)

---

## Monitoring & Metrics

### Lightning Store Integration

APO logs optimization rounds to Lightning Store:

```python
# Each round creates a rollout in Lightning Store
{
  "input": {
    "optimization_round": 3,
    "score": 0.82,
    "prompt": "Optimized prompt..."
  },
  "mode": "val",
  "metadata": {
    "source": "apo_optimization",
    "round": 3,
    "timestamp": "2024-01-15T14:33:15Z"
  }
}
```

### Prometheus Metrics

APO manager exposes metrics via Lightning Store's PrometheusMetricsBackend:

```prometheus
# Optimization rounds completed
apo_optimization_rounds_total{run_id="apo_run_20240115_143022"}

# Best score achieved
apo_best_score{run_id="apo_run_20240115_143022"}

# LLM API calls made
apo_llm_calls_total{operation="gradient|edit|evaluate"}

# Optimization duration
apo_optimization_duration_seconds
```

### Grafana Dashboard

Add APO panel to Lightning Server dashboard:

```json
{
  "title": "APO Optimization Progress",
  "targets": [
    {
      "expr": "apo_best_score",
      "legendFormat": "Best Score"
    }
  ],
  "yaxes": [
    {
      "label": "Score",
      "min": 0,
      "max": 1
    }
  ]
}
```

---

## Best Practices

### 1. **Validation Example Quality**

✅ **Do:**
- Use 20-50 diverse examples minimum
- Cover edge cases and failure modes
- Include examples of varying difficulty
- Use real-world data when possible

❌ **Don't:**
- Use synthetic or trivial examples only
- Overlap validation with training data
- Include inconsistent or contradictory examples

### 2. **Initial Prompt Design**

✅ **Do:**
- Start with a reasonable baseline prompt
- Include task definition and constraints
- Provide 1-2 examples in the prompt if helpful

❌ **Don't:**
- Start with a completely blank prompt
- Over-specify implementation details
- Include irrelevant or verbose instructions

### 3. **Hyperparameter Tuning**

✅ **Do:**
- Start with balanced settings (beam_width=4, rounds=5)
- Increase beam width if quality plateaus
- Use early stopping to save cost

❌ **Don't:**
- Max out all parameters without testing
- Run too many rounds (diminishing returns after 5-10)
- Ignore cost vs. quality trade-offs

### 4. **Gradient Model Selection**

✅ **Do:**
- Use strong reasoning models (GPT-4, Claude Opus)
- Test with your task domain (code, text, etc.)
- Consider cost vs. quality

❌ **Don't:**
- Use weak models (<7B params) for gradients
- Mix model providers mid-optimization
- Use models with different instruction formats

### 5. **Iteration Strategy**

✅ **Do:**
- Run multiple short optimizations (5 rounds each)
- Manually inspect intermediate prompts
- Combine APO with human refinement

❌ **Don't:**
- Blindly trust optimized prompts
- Skip validation on held-out test set
- Over-optimize (overfitting to validation)

---

## Troubleshooting

### Issue: Optimization Not Improving

**Symptoms:**
- Best score stays flat across rounds
- Beam variations are very similar

**Solutions:**
1. **Increase diversity temperature:**
   ```bash
   APO_DIVERSITY_TEMPERATURE=1.0  # More variation
   ```

2. **Use stronger gradient model:**
   ```bash
   APO_GRADIENT_MODEL=gpt-4  # Better feedback
   ```

3. **Check validation examples:**
   - Are they too easy/hard?
   - Do they cover diverse scenarios?

4. **Increase beam width:**
   ```bash
   APO_BEAM_WIDTH=8  # Explore more variations
   ```

---

### Issue: LLM API Errors

**Symptoms:**
- 429 Rate Limit errors
- Timeout errors during optimization

**Solutions:**
1. **Reduce batch size:**
   ```bash
   APO_GRADIENT_BATCH_SIZE=4  # Fewer concurrent calls
   ```

2. **Add retry logic** (already built-in to LLM client)

3. **Use LiteLLM proxy** for automatic fallback:
   ```bash
   VLLM_API_BASE=http://litellm-proxy:4000/v1
   ```

4. **Check rate limits:**
   ```bash
   # OpenAI Tier 1: 3,500 RPM
   # Batch size 8 × 5 rounds = 40 calls/minute ✅ OK
   ```

---

### Issue: High Cost

**Symptoms:**
- Optimization runs cost $5-$20+ per run

**Solutions:**
1. **Use cheaper models:**
   ```bash
   APO_GRADIENT_MODEL=gpt-4o-mini  # $0.15/1M tokens
   ```

2. **Reduce beam parameters:**
   ```bash
   APO_BEAM_WIDTH=3
   APO_BEAM_ROUNDS=3
   ```

3. **Early stopping:**
   ```json
   {
     "earlyStoppingThreshold": 0.02  # Stop sooner
   }
   ```

4. **Use vLLM for local inference:**
   ```bash
   VLLM_API_BASE=http://vllm-server:8000/v1
   APO_GRADIENT_MODEL=meta-llama/Llama-3-70B-Instruct
   ```

---

### Issue: Out of Memory (OOM)

**Symptoms:**
- APO manager crashes during optimization
- Docker container restarts

**Solutions:**
1. **Reduce batch size:**
   ```bash
   APO_GRADIENT_BATCH_SIZE=1  # Sequential processing
   ```

2. **Increase container memory:**
   ```yaml
   # docker-compose.yml
   lightning-server:
     deploy:
       resources:
         limits:
           memory: 4G  # Increase from 2G
   ```

3. **Use external LLM service** (OpenAI, not local vLLM):
   ```bash
   OPENAI_API_BASE=https://api.openai.com/v1
   ```

---

### Issue: Prompts Not Converging

**Symptoms:**
- Each round produces completely different prompts
- No clear improvement trajectory

**Solutions:**
1. **Lower diversity temperature:**
   ```bash
   APO_DIVERSITY_TEMPERATURE=0.5  # More conservative edits
   ```

2. **Check validation examples:**
   - Are expected outputs consistent?
   - Do examples contradict each other?

3. **Review gradient feedback manually:**
   ```python
   # Enable debug logging
   LOG_LEVEL=DEBUG
   ```

4. **Simplify task description:**
   - Make success criteria more specific
   - Provide clearer constraints

---

## Examples

### Example 1: Customer Support Summarization

**Task:** Summarize customer support tickets in 2-3 sentences.

**Initial Prompt:**
```
You are a customer support agent. Summarize the ticket.
```

**Validation Examples:**
```json
[
  {
    "input": "Customer: My order #12345 hasn't arrived yet. It's been 2 weeks. Tracking says delivered but I didn't receive it. Please help!",
    "expected_output": "Order #12345 marked as delivered but not received by customer after 2 weeks. Customer requests investigation and resolution."
  },
  {
    "input": "User: I can't login after resetting my password. It says 'invalid credentials' even though I just set a new one.",
    "expected_output": "Login failure after password reset. System rejecting new credentials despite successful reset confirmation."
  }
]
```

**Optimization:**
```bash
curl -X POST http://localhost:9090/api/apo/initialize \
  -H "Content-Type: application/json" \
  -d @ticket_summarization.json

curl -X POST http://localhost:9090/api/apo/optimize \
  -d '{"numRounds": 5}'
```

**Optimized Prompt (After 5 rounds):**
```
You are an expert customer support analyst. Summarize customer support tickets in exactly 2-3 concise sentences.

Focus on:
1. Core issue or request
2. Relevant order/account numbers
3. Customer's desired outcome

Use professional, neutral tone. Avoid speculation.
```

**Score Improvement:** 0.63 → 0.89 (+41%)

---

### Example 2: Code Review Comments

**Task:** Generate helpful code review comments.

**Initial Prompt:**
```
Review this code and provide feedback.
```

**Validation Examples:**
```json
[
  {
    "input": "def calculate(x, y):\n    return x + y",
    "expected_output": "Function lacks type hints and docstring. Consider adding parameter validation for edge cases."
  },
  {
    "input": "async def fetch_data(url):\n    response = requests.get(url)\n    return response.json()",
    "expected_output": "Function is marked async but doesn't await. Use httpx or aiohttp for true async requests. Add error handling for network failures."
  }
]
```

**Optimized Prompt:**
```
You are a senior software engineer performing code review. Provide constructive feedback on:

1. **Correctness** - Bugs, logic errors, edge cases
2. **Best Practices** - Idiomatic patterns, type safety
3. **Performance** - Efficiency, async/await usage
4. **Maintainability** - Naming, documentation, clarity

Format each comment with:
- Issue description
- Specific suggestion or fix
- Severity (critical/major/minor)

Focus on actionable improvements. Avoid nitpicking.
```

**Score Improvement:** 0.58 → 0.84 (+45%)

---

### Example 3: SQL Query Generation

**Task:** Generate SQL queries from natural language.

**Initial Prompt:**
```
Convert the question to SQL.
```

**Validation Examples:**
```json
[
  {
    "input": "Show me all users who signed up last month",
    "expected_output": "SELECT * FROM users WHERE signup_date >= DATE_TRUNC('month', CURRENT_DATE - INTERVAL '1 month') AND signup_date < DATE_TRUNC('month', CURRENT_DATE);"
  },
  {
    "input": "Count active subscriptions by plan type",
    "expected_output": "SELECT plan_type, COUNT(*) as active_count FROM subscriptions WHERE status = 'active' GROUP BY plan_type ORDER BY active_count DESC;"
  }
]
```

**Optimized Prompt:**
```
You are an expert SQL query generator. Convert natural language questions to syntactically correct PostgreSQL queries.

Rules:
1. Use explicit column names (avoid SELECT *)
2. Include WHERE clauses for filters
3. Add ORDER BY for listings
4. Use proper date functions (DATE_TRUNC, INTERVAL)
5. Always end with semicolon

Schema context:
- users (id, email, signup_date, status)
- subscriptions (id, user_id, plan_type, status, created_at)
- orders (id, user_id, total, created_at)

Output only the SQL query, no explanation.
```

**Score Improvement:** 0.51 → 0.87 (+71%)

---

## Integration with VERL

APO and VERL can be used together for end-to-end LLM optimization:

### Workflow: Prompt → Training → Production

```
┌─────────────────────────────────────────────────────────┐
│             APO + VERL Combined Workflow                 │
└─────────────────────────────────────────────────────────┘

Step 1: APO Prompt Optimization
  ↓
  Optimize system prompt for task
  ↓
  Best Prompt (Score: 0.87)

Step 2: VERL RLHF Training
  ↓
  Fine-tune LLM with optimized prompt
  ↓
  Trained Model (Higher quality responses)

Step 3: Production Deployment
  ↓
  Deploy optimized prompt + trained model
  ↓
  Monitor performance in Lightning Store
```

### Example Code

```python
import requests

API = "http://localhost:9090"

# Step 1: Optimize prompt with APO
requests.post(f"{API}/api/apo/initialize", json={
    "initialPrompt": "You are a helpful assistant...",
    "taskDescription": "Summarize customer tickets",
    "validationExamples": validation_examples
})

apo_result = requests.post(f"{API}/api/apo/optimize", json={
    "numRounds": 5
}).json()

best_prompt = apo_result["best_prompt"]

# Step 2: Train with VERL using optimized prompt
requests.post(f"{API}/api/verl/initialize", json={
    "actorModel": "meta-llama/Llama-3-8B-Instruct",
    "rewardModel": "reward_model_path"
})

verl_result = requests.post(f"{API}/api/verl/train/start", json={
    "datasetPath": "/data/training_dataset.jsonl",
    "numSteps": 1000
}).json()

# Step 3: Use optimized prompt + trained model in production
final_model = verl_result["checkpoint_path"]
```

---

## Summary

APO (Automatic Prompt Optimization) provides:

✅ **Automated prompt engineering** via beam search and LLM gradients  
✅ **Data-driven improvements** validated on held-out examples  
✅ **Cost-effective** compared to manual iteration ($0.02-$2 per run)  
✅ **Reproducible** and trackable in Lightning Store  
✅ **Flexible** for any LLM backend (vLLM, OpenAI, Anthropic)  

**When to use APO:**
- You have 10-100+ validation examples
- Task definition is clear and stable
- You want reproducible prompt improvements
- You want to save time on manual prompt engineering

**Next steps:**
1. ✅ Prepare validation examples for your task
2. ✅ Configure APO in `.env` file
3. ✅ Initialize optimizer via `/api/apo/initialize`
4. ✅ Start optimization via `/api/apo/optimize`
5. ✅ Monitor progress and retrieve best prompt
6. ✅ Deploy optimized prompt to production

For questions or issues, see [Troubleshooting](#troubleshooting) section above.

**Happy optimizing!** 🚀✨
