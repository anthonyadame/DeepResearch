# Docker Guide for DeepResearch

**Updated**: 2026-02-20  
**Status**: ✅ 4-Stack Unified Architecture ACTIVE  
**Architecture**: 4 Separate Stacks on Unified Network (deepresearch-hub)  
**Production Ready**: Yes

---

## Quick Start - Using Deployment Scripts

### Windows (PowerShell)

```powershell
cd Docker

# Start all 4 stacks (creates network automatically)
./deploy-stacks.ps1 -Action start

# Start with automatic file creation if missing
./deploy-stacks.ps1 -Action start -CreateMissing

# Check status
./deploy-stacks.ps1 -Action status

# View health
./deploy-stacks.ps1 -Action health

# View logs
./deploy-stacks.ps1 -Action logs -Stack core
./deploy-stacks.ps1 -Action logs -Stack ai -Follow

# Stop all stacks
./deploy-stacks.ps1 -Action stop

# Full validation
./deploy-stacks.ps1 -Action validate
```

### Linux/Mac (Bash)

```bash
cd Docker

# Start all 4 stacks
./deploy-stacks.sh start

# Check status
./deploy-stacks.sh status

# View health
./deploy-stacks.sh health

# View logs
./deploy-stacks.sh logs core
./deploy-stacks.sh logs ai
./deploy-stacks.sh logs websearch
./deploy-stacks.sh logs monitoring

# Stop all stacks
./deploy-stacks.sh stop

# Full validation
./deploy-stacks.sh validate
```

### Manual Deployment (Advanced)

```bash
# Create network (automatic in scripts)
docker network create deepresearch-hub --driver bridge

# Start stacks in order
docker-compose -f Docker/docker-compose.core.yml up -d
docker-compose -f Docker/docker-compose.websearch.yml up -d
docker-compose -f Docker/docker-compose.ai.yml up -d
docker-compose -f Docker/Observability/docker-compose-monitoring.yml up -d

docker-compose -f Docker/Observability/docker-compose-monitoring.yml up -d

# Verify all services
docker ps | grep deepresearch
```

## Script Parameters & Options

### PowerShell Script (`deploy-stacks.ps1`)

**Parameters:**
- `-Action`: Operation to perform (start, stop, restart, status, logs, health, validate, cleanup)
- `-Stack`: Which stack(s) to target (all, core, ai, websearch, monitoring)  
- `-Follow`: For logs action, continuously follow logs
- `-Tail`: Number of log lines to show (default: 50)
- `-CreateMissing`: Create placeholder docker-compose files if they don't exist

**Examples:**
```powershell
./deploy-stacks.ps1 -Action start -Stack all
./deploy-stacks.ps1 -Action status -Stack core
./deploy-stacks.ps1 -Action logs -Stack ai -Follow -Tail 100
./deploy-stacks.ps1 -Action start -CreateMissing  # Creates missing compose files interactively
./deploy-stacks.ps1 -Action health              # Check health of all stacks
```

### Bash Script (`deploy-stacks.sh`)

**Note**: Bash script may have compatibility issues on WSL2. Recommended to use PowerShell script on Windows.

**Usage:**
```bash
./deploy-stacks.sh [ACTION] [STACK] [OPTIONS]
```

**Examples:**
```bash
./deploy-stacks.sh start all           # Start all stacks
./deploy-stacks.sh status core         # Show core stack status
./deploy-stacks.sh logs ai -f          # Follow AI stack logs
```

## Access Points

### Core Services
- **API**: http://localhost:5000
- **Health Check**: http://localhost:5000/health
- **Swagger UI**: http://localhost:5000/swagger/ui/index.html
- **Redis CLI**: `docker-compose -f Docker/docker-compose.core.yml exec redis redis-cli`
- **InfluxDB**: http://localhost:8086

### AI Services
- **Ollama**: http://localhost:11434/api/tags
- **Qdrant**: http://localhost:6333/health
- **Lightning Server**: http://localhost:8090

### Websearch Services  
- **SearXNG**: http://localhost:8080
- **Crawl4AI**: http://localhost:11235/crawl
- **Caddy Reverse Proxy**: http://localhost:80 (HTTP)

### Monitoring Services
- **Prometheus**: http://localhost:9090
- **Grafana**: http://localhost:3001 (admin/admin)
- **AlertManager**: http://localhost:9093
- **Jaeger UI**: http://localhost:16686
- **OTEL Collector**: http://localhost:13133 (health)

## Stack Composition

### Stack 1: Core Services

**File**: `docker-compose.core.yml`  
**Services**: 5  
**Network**: deepresearch-hub  
**GPU Required**: No

1. **deepresearch-api** (.NET 8.0 ASP.NET Core) - port 5000
   - Main API for research operations
   - Depends on: redis, influxdb
   - Connects to: ollama, qdrant, lightning, searxng, crawl4ai

2. **deep-research-agent** (Console application)
   - CLI interface for agent operations
   - Depends on: redis

3. **redis** (Redis 7.0) - port 6379
   - Distributed cache and session store
   - Persistent storage: redis-data volume

4. **redis-exporter** (Prometheus Redis Exporter) - port 9121
   - Exports Redis metrics for Prometheus
   - Monitored by: prometheus scrape job

5. **influxdb** (InfluxDB 2.0) - port 8086
   - Time-series database for metrics
   - Persistent storage: influxdb-data volume

### Stack 2: AI Services

**File**: `docker-compose.ai.yml`  
**Services**: 3  
**Network**: deepresearch-hub  
**GPU Required**: Yes (NVIDIA)  
**GPU Allocation**:
- Ollama: GPU 0,1,2 (multiple GPUs for large models)
- Lightning: GPU 3 (separate for orchestration)

1. **ollama** (Ollama LLM Inference) - port 11434
   - Large Language Model inference engine
   - Models: Customizable via environment
   - GPU: Primary (0,1,2)
   - Persistent storage: ollama_data volume

2. **qdrant** (Qdrant Vector Database) - port 6333
   - Vector storage for embeddings and semantic search
   - Persistent storage: qdrant_storage volume

3. **lightning-server** (Lightning Orchestration) - port 8090
   - Agent framework orchestration
   - Supports: APO (Automatic Prompt Optimization), VERL (Volcano Engine Reinforcement Learning)
   - GPU: Dedicated (device 3)
   - Persistent storage: lightning-data volume

### Stack 3: Websearch Services

**File**: `docker-compose.websearch.yml`  
**Services**: 3  
**Network**: deepresearch-hub  
**GPU Required**: No

1. **caddy** (Caddy Reverse Proxy) - ports 80/443
   - Unified reverse proxy for all web traffic
   - Routes:
     - /search → searxng:8080
     - /crawl → crawl4ai:11235
     - /api → deepresearch-api:5000 (via network)
   - Features: TLS termination, compression, middleware
   - Persistent storage: caddy-data, caddy-config volumes

2. **searxng** (SearXNG Meta Search) - port 8080
   - Meta search engine aggregating results from multiple sources
   - Independent scaling possible
   - Persistent storage: searxng-data volume

3. **crawl4ai** (Crawl4AI Web Scraper) - port 11235
   - Intelligent web scraping and content extraction
   - Browser automation support
   - Persistent storage: crawl4ai_profiles volume

### Stack 4: Monitoring Services

**File**: `docker-compose-monitoring.yml`  
**Services**: 5  
**Network**: deepresearch-hub  
**GPU Required**: No

1. **prometheus** (Prometheus) - port 9090
   - Metrics collection and time-series database
   - Scrape targets: 4 (redis-exporter, core, websearch, ai)
   - Retention: 15 days (configurable)
   - Persistent storage: prometheus-data volume

2. **grafana** (Grafana) - port 3001
   - Metrics visualization and dashboarding
   - Default credentials: admin/admin (CHANGE IN PRODUCTION)
   - Data source: Prometheus
   - Persistent storage: grafana-data volume

3. **alertmanager** (Alertmanager) - port 9093
   - Alert routing and management
   - Integrations: Email, PagerDuty, Slack, Webhooks
   - Persistent storage: alertmanager-data volume

4. **jaeger** (Jaeger UI) - port 16686
   - Distributed tracing visualization
   - Traces from: OTEL Collector
   - Persistent storage: jaeger-data volume

5. **otel-collector** (OpenTelemetry Collector) - ports 4317/4318, 8889, 13133
   - Telemetry collection and processing
   - Receivers: OTLP (gRPC/HTTP), metrics, traces
   - Exporters: Prometheus (:8889), Zipkin (jaeger), Debug
   - Health check: :13133

---

## Stack Composition (Detailed Table)

## Docker Compose Files (4-Stack Architecture)

### Orchestration & Automation Scripts

**Docker/deploy-stacks.ps1** (PowerShell - 450+ lines)  
**Docker/deploy-stacks.sh** (Bash - 420+ lines)

Unified scripts for managing all 4 stacks:
- **Actions**: start, stop, restart, status, logs, health, validate, cleanup
- **Stacks**: core, ai, websearch, monitoring (or 'all')
- **Examples**:
  ```powershell
  ./deploy-stacks.ps1 -Action start                 # Start all
  ./deploy-stacks.ps1 -Action status                # Status all
  ./deploy-stacks.ps1 -Action logs -Stack ai        # AI logs
  ./deploy-stacks.ps1 -Action health -Stack core    # Core health
  ```

### Core Stack

**File**: `Docker/docker-compose.core.yml`  
**Purpose**: Foundational services (API, cache, database)  
**Commands**:
```bash
docker-compose -f Docker/docker-compose.core.yml up -d    # Start
docker-compose -f Docker/docker-compose.core.yml down      # Stop
docker-compose -f Docker/docker-compose.core.yml logs -f   # Logs
```

### AI Stack

**File**: `Docker/docker-compose.ai.yml`  
**Purpose**: Machine learning inference and vector storage  
**GPU Enabled**: Yes (nvidia-runtime)  
**Commands**:
```bash
docker-compose -f Docker/docker-compose.ai.yml up -d       # Start
nvidia-smi                                                  # Monitor GPU
docker-compose -f Docker/docker-compose.ai.yml logs ollama # Ollama logs
```

### Websearch Stack

**File**: `Docker/docker-compose.websearch.yml`  
**Purpose**: Web discovery and reverse proxy  
**Commands**:
```bash
docker-compose -f Docker/docker-compose.websearch.yml up -d
curl http://localhost:8080/stats  # SearXNG health
curl http://localhost:80          # Caddy health
```

### Monitoring Stack

**File**: `Docker/Observability/docker-compose-monitoring.yml`  
**Purpose**: Observability, metrics, tracing, alerting  
**Commands**:
```bash
docker-compose -f Docker/Observability/docker-compose-monitoring.yml up -d
curl http://localhost:9090/health     # Prometheus health
curl http://localhost:3001            # Grafana health
```

## Configuration

### Environment Files (Stack-Specific)

**Docker/.env** (Core Stack)
```env
# Core API Configuration
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:5000
Swagger__Enabled=true
HttpsRedirection__Enabled=false

# JWT Authentication  
Jwt__SecretKey=your-secret-key-minimum-32-characters
Jwt__Issuer=deepresearch-api
Jwt__Audience=deepresearch-api
Jwt__ExpirationMinutes=60

# Database
INFLUXDB_USERNAME=admin
INFLUXDB_PASSWORD=password
INFLUXDB_ORG=deep-research
INFLUXDB_BUCKET=research
INFLUXDB_TOKEN=influx-token
```

**Docker/AI/.env** (AI Stack)
```env
# Ollama Configuration
OLLAMA_HOST=0.0.0.0:11434
OLLAMA_MODELS_PATH=/root/.ollama/models

# Qdrant Configuration
QDRANT_API_KEY=default-key
QDRANT_PREFER_GRPC=false

# Lightning Configuration
LIGHTNING_PORT=8090
LIGHTNING_LOG_LEVEL=INFO
```

**Docker/Websearch/.env** (Websearch Stack)
```env
# SearXNG Configuration
SEARXNG_HOSTNAME=localhost
SEARXNG_BASE_URL=https://localhost/
SEARXNG_SECRET_KEY=your-secret-key

# Crawl4AI Configuration
CRAWL4AI_PROFILES_PATH=./Docker/Websearch/crawl4ai_profiles
CRAWL4AI_LOG_LEVEL=INFO
CRAWL4AI_API_PORT=11235

# Caddy Configuration
CADDY_ADMIN=0.0.0.0:2019
```

**Docker/Observability/.env** (Monitoring Stack)
```env
# Prometheus Configuration
PROMETHEUS_RETENTION=15d
PROMETHEUS_SCRAPE_INTERVAL=15s

# Grafana Configuration
GF_SECURITY_ADMIN_USER=admin
GF_SECURITY_ADMIN_PASSWORD=admin
GF_USERS_ALLOW_SIGN_UP=false
GF_LOG_LEVEL=info

# AlertManager Configuration
ALERTMANAGER_PORT=9093
```

### Network Configuration

**Unified Network**: `deepresearch-hub` (bridge)
```bash
# View network
docker network inspect deepresearch-hub

# Service-to-Service Communication (DNS):
# http://service-name:port
# Examples:
# - http://redis:6379        (from core)
# - http://ollama:11434      (from core to AI)
# - http://searxng:8080      (from core to websearch)
```

## Port Reference

| Port | Service | Stack | Purpose |
|------|---------|-------|---------|
| **5000** | deepresearch-api | Core | REST API |
| **6379** | redis | Core | Cache/Session Store |
| **8086** | influxdb | Core | Time-Series DB |
| **9121** | redis-exporter | Core | Prometheus Metrics |
| **11434** | ollama | AI | LLM Inference |
| **6333** | qdrant | AI | Vector Database |
| **8090** | lightning-server | AI | Orchestration |
| **8080** | searxng | Websearch | Meta Search |
| **11235** | crawl4ai | Websearch | Web Scraper |
| **80/443** | caddy | Websearch | Reverse Proxy |
| **9090** | prometheus | Monitoring | Metrics DB |
| **3001** | grafana | Monitoring | Dashboards |
| **9093** | alertmanager | Monitoring | Alert Router |
| **16686** | jaeger | Monitoring | Tracing UI |
| **4317** | otel-collector | Monitoring | OTLP gRPC |
| **4318** | otel-collector | Monitoring | OTLP HTTP |
| **8889** | otel-collector | Monitoring | Prometheus Export |
| **13133** | otel-collector | Monitoring | Health Check |

## Networking

### Network Architecture

```
┌──────────────────────────────────────────────┐
│    Unified Bridge Network: deepresearch-hub  │
├──────────────────────────────────────────────┤
│                                              │
│  ┌──────────────┐  ┌──────────────┐         │
│  │ Core Stack   │  │ AI Stack     │         │
│  │ (API, Redis) │◄─┤ (Ollama, Q)  │         │
│  └──────┬───────┘  └──────┬───────┘         │
│         │                 │                 │
│         └─────────┬───────┘                 │
│                   │                         │
│         ┌─────────▼────────┐               │
│         │ Websearch Stack  │               │
│         │ (SearXNG, Caddy) │               │
│         └──────────────────┘               │
│                                            │
│         ┌──────────────────┐               │
│         │ Monitoring Stack │               │
│         │ (Prometheus,GF)  │               │
│         └──────────────────┘               │
│                                            │
└──────────────────────────────────────────────┘
```

### Cross-Stack Communication

**All services accessible via DNS**:
- API calls Ollama: `http://ollama:11434`
- API calls SearXNG: `http://searxng:8080`
- Prometheus scrapes Redis: `http://redis-exporter:9121`
- Prometheus scrapes AI services: `http://ollama:11434`, `http://qdrant:6333`

No IP address hardcoding needed - Docker DNS handles resolution

## Common Operations

### Deployment (New 4-Stack Approach)

**Recommended**: Use automation scripts

```powershell
# PowerShell (Windows)
cd Docker
./deploy-stacks.ps1 -Action start  # Start all 4 stacks
./deploy-stacks.ps1 -Action status # Check status
./deploy-stacks.ps1 -Action health # Health check
```

```bash
# Bash (Linux/Mac)
cd Docker
./deploy-stacks.sh start            # Start all 4 stacks
./deploy-stacks.sh status           # Check status
./deploy-stacks.sh health           # Health check
```

### Manual Deployment

```bash
# 1. Create network (if not exists)
docker network create deepresearch-hub --driver bridge

# 2. Start stacks in order (dependencies first)
docker-compose -f Docker/docker-compose.core.yml up -d
docker-compose -f Docker/docker-compose.websearch.yml up -d
docker-compose -f Docker/docker-compose.ai.yml up -d
docker-compose -f Docker/Observability/docker-compose-monitoring.yml up -d

# 3. Verify all services
docker ps | grep deepresearch
docker network inspect deepresearch-hub

# 4. Check health
curl http://localhost:5000/health
curl http://localhost:9090/health
curl http://localhost:3001/api/health
```

### Stack Management

```bash
# Start individual stack
docker-compose -f Docker/docker-compose.core.yml up -d

# Stop stack (data persists)
docker-compose -f Docker/docker-compose.core.yml down

# Stop and remove volumes (destructive)
docker-compose -f Docker/docker-compose.core.yml down -v

# View logs
docker-compose -f Docker/docker-compose.core.yml logs -f [service-name]

# Execute commands in container
docker-compose -f Docker/docker-compose.core.yml exec redis redis-cli ping
```

## Troubleshooting

### Quick Diagnosis

```powershell
# Check all containers
docker ps
docker ps -a  # Including stopped

# Check network
docker network ls
docker network inspect deepresearch-hub

# Check volumes
docker volume ls

# Check system
docker system df  # Disk usage
docker stats      # CPU/Memory usage
```

### Common Issues

#### Containers Won't Start

```bash
# View error logs
docker logs [container-name]
docker-compose -f Docker/docker-compose.core.yml logs

# Common causes and fixes:
# 1. Port already in use
netstat -ano | findstr :5000    # Windows
lsof -i :5000                   # Linux

# 2. Insufficient disk space
docker system df
docker system prune -a

# 3. Network issues
docker network ls
docker network create deepresearch-hub --driver bridge  # Recreate if missing

# 4. Out of memory
docker stats
# Check resource limits in compose files
```

#### Services Can't Communicate

```bash
# Test DNS resolution
docker-compose -f Docker/docker-compose.core.yml exec api \
  nslookup ollama

# Test connectivity
docker-compose -f Docker/docker-compose.core.yml exec api \
  curl http://ollama:11434/api/tags

# Check network attachment
docker network inspect deepresearch-hub | grep -A 10 "Containers"
```

#### Monitoring Not Working

```bash
# Check Prometheus targets
curl http://localhost:9090/api/v1/targets

# Check OTEL Collector
curl http://localhost:13133

# Check Redis Exporter reachability
docker-compose -f Docker/Observability/docker-compose-monitoring.yml exec prometheus \
  curl http://redis-exporter:9121/metrics
```

#### GPU Not Detected

```powershell
# Verify GPU available
nvidia-smi
docker run --rm --gpus all nvidia/cuda:12.0-runtime nvidia-smi

# Check Docker runtime
docker info | grep nvidia

# Check container GPU access
docker-compose -f Docker/docker-compose.ai.yml exec ollama nvidia-smi
```

### Advanced Troubleshooting

```bash
# Inspect container configuration
docker inspect [container-name] | jq '.[] | {Env, Mounts, NetworkSettings}'

# Check container health status
docker inspect [container-name] | jq '.[] | .State.Health'

# Monitor container performance
docker stats [container-name] --no-stream

# Execute shell in container
docker-compose exec [service-name] sh
docker-compose exec ollama /bin/bash

# Check service dependencies
docker-compose config | grep "depends_on" -A 5
```

## Health Checks

### Verify Service Health

```bash
# Use deployment script
./deploy-stacks.ps1 -Action health

# Manual checks:
curl http://localhost:5000/health           # API
docker-compose exec redis redis-cli ping    # Redis
curl http://localhost:9090/health           # Prometheus
curl http://localhost:3001/api/health       # Grafana
curl http://localhost:11434/api/tags        # Ollama
curl http://localhost:6333/health           # Qdrant
curl http://localhost:13133                 # OTEL Collector
```

### Check Logs for Errors

```bash
# All stacks
docker-compose -f Docker/docker-compose.core.yml logs | grep -i error
docker-compose -f Docker/docker-compose.ai.yml logs | grep -i error
docker-compose -f Docker/docker-compose.websearch.yml logs | grep -i error
docker-compose -f Docker/Observability/docker-compose-monitoring.yml logs | grep -i error

# Specific service
docker-compose -f Docker/docker-compose.core.yml logs deepresearch-api | tail -50
docker logs [container-name] --tail 100 --follow
```

## Data & Backup

### Database Persistence

**Volumes Created**:
- redis-data: Redis cache
- qdrant_storage: Vector database
- influxdb-data: Time-series metrics
- prometheus-data: Prometheus metrics
- grafana-data: Grafana dashboards
- ollama_data: LLM models

**Check Volume Usage**:
```bash
docker volume ls
docker system df -v | grep deepresearch
```

### Backup Procedure

```bash
# Backup volumes
docker run --rm -v redis-data:/data -v /backups:/backups \
  busybox tar czf /backups/redis-backup.tar.gz -C / data

# Backup database
docker-compose exec redis redis-cli BGSAVE
docker cp deepresearch-redis:/data/dump.rdb ./redis-dump.rdb

# Backup Qdrant
docker cp deepresearch-qdrant:/qdrant/storage ./qdrant-backup
```

## Documentation & Resources

### Official References
- Docker: https://docs.docker.com/
- Docker Compose: https://docs.docker.com/compose/
- ASP.NET Core: https://learn.microsoft.com/en-us/aspnet/core/
- Prometheus: https://prometheus.io/docs/
- Grafana: https://grafana.com/docs/

---

**Last Updated**: 2026-02-20  
**Status**: ✅ Operational  
**Stacks**: 2 (deepresearch + monitoring)  
**Documentation**: BuildDocs/Docker/
