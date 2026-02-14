# Lightning Server - Agent Orchestration

FastAPI server providing Agent-Lightning integration with APO & VERL support for the Deep Research Agent system.

## Features

- ✅ **Agent Lightning Store** - Distributed task/rollout management
- ✅ **APO (Automatic Performance Optimization)** - Resource-aware task scheduling
- ✅ **VERL (Verification and Reasoning Layer)** - Fact verification & reasoning validation
- ✅ **Web Dashboard** - Real-time monitoring UI
- ✅ **C# Integration** - Full compatibility with Deep Research Agent

## Quick Start

### Build and Run

# Build the Docker image
cd lightning-server
docker build -t lightning-server:cuda --target cuda .

# Run the server
docker run --gpus all -p 9090:9090 lightning-server:cuda

# Or specify a single GPU
docker run --gpus '"device=0"' -p 9090:9090 lightning-server:cuda

# Or specify multiple GPUs
docker run --gpus '"device=0,1"' -p 9090:9090 lightning-server:cuda

# Alternative env-based form
docker run -e NVIDIA_VISIBLE_DEVICES=0 -p 9090:9090 lightning-server:cuda

# Compose snippet
```
services:
  lightning-server:
    image: lightning-server:cuda
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: 1          # or 2
              capabilities: [gpu]
    ports:
      - "9090:9090"
````
# Access the dashboard
open http://localhost:9090/dashboard

# Or via docker-compose
docker-compose up lightning-server
### Access Points

- **API Documentation**: http://localhost:9090/docs
- **Dashboard**: http://localhost:9090/dashboard
- **Health Check**: http://localhost:9090/health
- **Server Info**: http://localhost:9090/api/server/info

## Dashboard

The Agent Lightning Dashboard provides real-time visibility into:

- Active rollouts and their status
- Agent registrations and capabilities
- Task queue depth and processing rates
- VERL verification metrics
- APO resource utilization

### Dashboard Features

- 📊 **Real-time Metrics** - Live updates on task execution
- 🔍 **Trace Viewer** - Inspect reasoning chains and spans
- 📈 **Performance Analytics** - APO optimization insights
- ✅ **Verification Reports** - VERL confidence scores

## API Endpoints

### Agent Management
- `POST /api/agents/register` - Register an agent
- `GET /api/agents/{agent_id}` - Get agent details
- `GET /api/agents` - List all agents

### Task Management
- `POST /api/tasks/submit` - Submit a task
- `GET /api/agents/{agent_id}/tasks/pending` - Get pending tasks
- `PUT /api/tasks/{task_id}/status` - Update task status

### VERL Endpoints
- `POST /api/verl/verify` - Verify task result
- `POST /api/verl/validate-reasoning` - Validate reasoning chain
- `POST /api/verl/evaluate-confidence` - Evaluate confidence score
- `POST /api/verl/verify-facts` - Verify facts
- `POST /api/verl/check-consistency` - Check statement consistency

## Environment Variables
LIGHTNING_PORT=9090                    # Server port APO_ENABLED=true                       # Enable APO VERL_ENABLED=true                      # Enable VERL APO_STRATEGY=balanced                  # high_performance|balanced|low_resource APO_MAX_TASKS=10                       # Max concurrent tasks VERL_CONFIDENCE_THRESHOLD=0.7          # VERL confidence threshold LOG_LEVEL=INFO                         # Logging level

## Development

### Local Setup
Install dependencies
pip install -r requirements.txt
Run locally
python server.py
### Testing
Test health endpoint
curl http://localhost:9090/health
Access dashboard
open http://localhost:9090/dashboard
View API docs
open http://localhost:9090/docs

## Architecture
┌─────────────────────────────────────────┐ │         Lightning Server (FastAPI)      │ ├─────────────────────────────────────────┤ │  ┌────────────┐    ┌──────────────┐    │ │  │   APO      │    │    VERL      │    │ │  │ Optimizer  │    │  Verifier    │    │ │  └────────────┘    └──────────────┘    │ ├─────────────────────────────────────────┤ │       Agent Lightning Store             │ │   (InMemoryLightningStore)              │ ├─────────────────────────────────────────┤ │         Dashboard (Static UI)           │ └─────────────────────────────────────────┘

## Integration with C# Deep Research Agent

var options = new LightningStoreOptions { LightningServerUrl = "http://localhost:9090", UseLightningServer = true };
var store = new LightningStore(options); await store.SaveFactsAsync(facts);

## Troubleshooting

### Dashboard Not Loading

Check if dashboard assets were built
ls /app/agentlightning/dashboard
View server logs
docker logs deep-research-lightning-server

### Lightning Store Unavailable

Verify server is running
curl http://localhost:9090/health
Check Agent Lightning version
curl http://localhost:9090/api/server/info | jq .agentLightningVersion

## License

MIT License - See LICENSE file for details
Now build and test:
