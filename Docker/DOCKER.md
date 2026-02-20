# Docker Guide for DeepResearch

**Updated**: 2026-02-20  
**Stack Names**: deepresearch (main), monitoring (observability)  
**Status**: ✅ Fully Operational

This guide explains how to build and run DeepResearch using Docker Compose with multiple stacks.

## Quick Start - Main Stack (deepresearch)

### Build and Run

```bash
# From project root: C:\RepoEx\PhoenixAI\DeepResearch
cd C:\RepoEx\PhoenixAI\DeepResearch

# Start all services
docker-compose -f Docker/docker-compose.yml up -d

# Check status
docker-compose -f Docker/docker-compose.yml ps

# View logs
docker-compose -f Docker/docker-compose.yml logs -f api
```

### Access the Application

- **API**: http://localhost:5000
- **Health Check**: http://localhost:5000/health
- **Swagger UI**: http://localhost:5000/swagger/ui/index.html

## Quick Start - Monitoring Stack (monitoring)

### Start Monitoring

```bash
# Start monitoring services
docker-compose -f Docker/Observability/docker-compose-monitoring.yml up -d

# Check status
docker-compose -f Docker/Observability/docker-compose-monitoring.yml ps
```

### Access Monitoring Services

- **Prometheus**: http://localhost:9090
- **Grafana**: http://localhost:3001 (admin/admin)
- **AlertManager**: http://localhost:9093
- **Jaeger**: http://localhost:16686

## Stack Composition

### Stack 1: DeepResearch (deepresearch)

**11 Services:**
- API: Deep Research API (.NET 8.0) - port 5000
- Agent: Deep Research Agent (console) - CLI interface
- Ollama: LLM inference service - port 11434
- Crawl4AI: Web scraping service - port 11235
- Lightning: Agent orchestration (APO/VERL) - port 8090
- Redis: Distributed cache - port 6379
- Qdrant: Vector database - port 6333
- InfluxDB: Time-series metrics - port 8086
- SearXNG: Meta search engine - port 8080
- Caddy: Reverse proxy/TLS - ports 80/443
- Redis-Exporter: Prometheus metrics - port 9121

**Network**: research-network (bridge)

### Stack 2: Monitoring (monitoring)

**6 Services:**
- Prometheus: Metrics collection - port 9090
- Grafana: Dashboard visualization - port 3001
- AlertManager: Alert routing - port 9093
- Jaeger: Distributed tracing - port 16686
- Monitoring API: Custom monitoring - port 5001
- Monitoring Service: Monitoring logic - port 8081

**Network**: monitoring_monitoring (bridge)

## Docker Compose Files

### Docker/docker-compose.yml (Main Stack)

**Stack Name**: `deepresearch`  
**Purpose**: Complete application with all services  
**Contexts**:
- API/Agent: context=.. dockerfile=Docker/DeepResearch/Dockerfile
- Lightning: context=.. dockerfile=Docker/lightning-server/Dockerfile

```bash
docker-compose -f Docker/docker-compose.yml up -d
```

### Docker/Observability/docker-compose-monitoring.yml (Monitoring)

**Stack Name**: `monitoring`  
**Purpose**: Observability, metrics, and tracing  
**Port Mapping Changes**:
- Grafana: 3001 (was 3000 - conflict with open-webui)
- API: 5001 (was 5000 - conflict with main API)

```bash
docker-compose -f Docker/Observability/docker-compose-monitoring.yml up -d
```

### Other Compose Files

- **Docker/docker-compose.api.yml**: API-only (lightweight development)
- **Docker/docker-compose.dev.yml**: Development overrides (hot reload)
- **Docker/docker-compose.prod.yml**: Production optimized

## Configuration

### Environment Files

**Docker/.env** (Main Stack):
```
SEARXNG_HOSTNAME=localhost
INFLUXDB_USERNAME=admin
INFLUXDB_PASSWORD=password
INFLUXDB_ORG=deep-research
INFLUXDB_BUCKET=research
INFLUXDB_TOKEN=influx-token
QDRANT_API_KEY=default-key
CRAWL4AI_PROFILES_PATH=./Docker/Websearch/crawl4ai_profiles
JWT_SECRET_KEY=your-super-secret-key-minimum-32-chars
LETSENCRYPT_EMAIL=admin@example.com
```

### Key Environment Variables

**API Settings:**
```
ASPNETCORE_ENVIRONMENT=Production|Development
ASPNETCORE_URLS=http://+:5000
Swagger__Enabled=true
HttpsRedirection__Enabled=false
```

**Authentication:**
```
Jwt__SecretKey=your-secret-key-minimum-32-characters
Jwt__Issuer=deepresearch-api
Jwt__Audience=deepresearch-api
Jwt__ExpirationMinutes=60
```

## Directory Structure

```
Docker/
├── docker-compose.yml              # Main orchestration (deepresearch)
├── docker-compose.api.yml          # API-only
├── docker-compose.dev.yml          # Development
├── docker-compose.prod.yml         # Production
├── .env                            # Configuration
│
├── DeepResearch/                   # API and Agent
│   └── Dockerfile                  # Multi-stage build
│
├── lightning-server/               # Agent orchestration
│   ├── Dockerfile                  # Python runtime
│   └── server.py                   # Application
│
├── Websearch/                      # Web search services
│   ├── crawl4ai-service/
│   ├── searxng/
│   └── crawl4ai_profiles/
│
└── Observability/                  # Monitoring stack
    ├── docker-compose-monitoring.yml
    ├── prometheus.yml
    ├── alerts.yml
    ├── alertmanager-config.yml
    ├── grafana-dashboard.json
    ├── grafana-datasource.yml
    └── otel-collector-config.yml

data/                               # Persistent data (runtime)
├── checkpoints/
├── keys/
└── logs/
```

## Dockerfiles

### Docker/DeepResearch/Dockerfile

Multi-stage build with:
- **Stage 1 (builder)**: SDK image, restores/builds
- **Stage 2 (publish)**: Publishes application
- **Stage 3 (runtime)**: Minimal runtime image

Build Context: Project root (..)  
Target: .NET 8.0 ASP.NET Core

### Docker/lightning-server/Dockerfile

Python-based build with:
- Node.js for dashboard
- Python 3.11 for Lightning server
- PyTorch with CUDA/CPU variants

Build Context: Project root (..)

## Common Commands

### Start/Stop Services

```bash
# Start all
docker-compose -f Docker/docker-compose.yml up -d

# Stop all
docker-compose -f Docker/docker-compose.yml down

# Restart service
docker-compose -f Docker/docker-compose.yml restart [service]

# View logs
docker-compose -f Docker/docker-compose.yml logs -f [service]

# Check status
docker-compose -f Docker/docker-compose.yml ps
```

### Build Commands

```bash
# Build images
docker-compose -f Docker/docker-compose.yml build

# Build without cache
docker-compose -f Docker/docker-compose.yml build --no-cache

# Build specific service
docker-compose -f Docker/docker-compose.yml build api
```

### Inspect

```bash
# View service logs
docker-compose logs api

# Execute command
docker-compose exec api curl http://localhost:5000/health

# Check stats
docker stats
```

## Port Reference

### DeepResearch Stack
- 5000: API
- 11434: Ollama
- 11235: Crawl4AI
- 8090: Lightning Server
- 6379: Redis
- 6333: Qdrant
- 8086: InfluxDB
- 8080: SearXNG
- 80/443: Caddy
- 9121: Redis-Exporter

### Monitoring Stack
- 9090: Prometheus
- 3001: Grafana
- 9093: AlertManager
- 16686: Jaeger
- 5001: Monitoring API
- 8081: Monitoring Service

## Deployment Options

### Development

```bash
# Fast development (API only)
docker-compose -f Docker/docker-compose.api.yml up -d

# Full stack with hot reload
docker-compose -f Docker/docker-compose.yml -f Docker/docker-compose.dev.yml up
```

### Production

```bash
# Production configuration
docker-compose -f Docker/docker-compose.prod.yml up -d

# With monitoring
docker-compose -f Docker/docker-compose.prod.yml up -d
docker-compose -f Docker/Observability/docker-compose-monitoring.yml up -d
```

## Troubleshooting

### Services Won't Start

```bash
# Check logs
docker-compose logs [service]

# Restart service
docker-compose restart [service]

# Rebuild image
docker-compose build --no-cache [service]
docker-compose up -d [service]
```

### Port Already in Use

- Grafana conflict (3000): Changed to 3001
- API conflict (5000): Monitoring API on 5001
- Update ports in docker-compose.yml if needed

### Health Checks Failing

Some services show "unhealthy" but are functional:
- Ollama, Qdrant, SearXNG: Health checks missing curl
- Monitoring Service: Endpoints not yet implemented

Services work correctly despite health check status.

### Data Persistence

Data stored in:
- `Docker/data/checkpoints/`: Workflow state
- `Docker/data/keys/`: Encryption keys
- `Docker/logs/`: Application logs
- Docker volumes for database services

## References

### Complete Documentation (BuildDocs/Docker/)

For comprehensive documentation, see:

- **[DOCKER_COMPREHENSIVE_GUIDE.md](../../BuildDocs/Docker/DOCKER_COMPREHENSIVE_GUIDE.md)**
  - Complete reference documentation (15,000+ words)
  - Full deployment guides
  - Advanced configuration

- **[DOCKER_INFRASTRUCTURE_INDEX.md](../../BuildDocs/Docker/DOCKER_INFRASTRUCTURE_INDEX.md)**
  - Master index for navigation
  - Quick access to all resources
  - Project structure overview

- **[MONITORING_STACK_DEPLOYMENT_REPORT.md](../../BuildDocs/Docker/MONITORING_STACK_DEPLOYMENT_REPORT.md)**
  - Monitoring stack configuration
  - Service access points
  - Monitoring troubleshooting

- **[DOCKER_VALIDATION_REPORT.md](../../BuildDocs/Docker/DOCKER_VALIDATION_REPORT.md)**
  - Deployment validation
  - Service health status
  - Known issues

- **[DOCUMENTATION_UPDATE_SUMMARY.md](../../BuildDocs/Docker/DOCUMENTATION_UPDATE_SUMMARY.md)**
  - Summary of changes
  - Path documentation
  - Quick command reference

### Official Documentation
- Docker: https://docs.docker.com/
- Docker Compose: https://docs.docker.com/compose/
- ASP.NET Core: https://learn.microsoft.com/en-us/aspnet/core/

### Project Documentation
- Complete Docker Guide: BuildDocs/Docker/DOCKER_COMPREHENSIVE_GUIDE.md
- Observability Phase 3: Docker/Observability/Phase3_Observability_Implementation.md
- Monitoring Plan: Docker/Observability/MONITORING_CONTAINER_SEPARATION_COMPLETE.md

---

**Last Updated**: 2026-02-20  
**Status**: ✅ Operational  
**Stacks**: 2 (deepresearch + monitoring)  
**Documentation**: BuildDocs/Docker/
