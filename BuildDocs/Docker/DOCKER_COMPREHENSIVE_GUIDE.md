# Docker Infrastructure Documentation - DeepResearch

**Last Updated**: 2026-02-20  
**Status**: ✅ Complete and Operational  
**Stacks**: deepresearch (11 services) + monitoring (6 services)

---

## Table of Contents

1. [Overview](#overview)
2. [Stack Architectures](#stack-architectures)
3. [Quick Start](#quick-start)
4. [Configuration](#configuration)
5. [Docker Compose Files](#docker-compose-files)
6. [Directory Structure](#directory-structure)
7. [Deployment Guides](#deployment-guides)
8. [Troubleshooting](#troubleshooting)

---

## Overview

DeepResearch uses Docker for containerized deployment across two primary stacks:

### Stack 1: DeepResearch (deepresearch)
- **Purpose**: Core application and supporting services
- **Services**: 11 (API, Agent, LLM, Search, Orchestration, Cache, Vector DB, Monitoring)
- **Compose File**: `Docker/docker-compose.yml`
- **Status**: ✅ Deployed and operational

### Stack 2: Monitoring (monitoring)
- **Purpose**: Observability, metrics, visualization, and distributed tracing
- **Services**: 6 (Prometheus, Grafana, AlertManager, Jaeger, API, Monitoring)
- **Compose File**: `Docker/Observability/docker-compose-monitoring.yml`
- **Status**: ✅ Deployed and operational

---

## Stack Architectures

### DeepResearch Stack (deepresearch)

```
┌─────────────────────────────────────────────────────────────┐
│              DeepResearch Stack (deepresearch)               │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  Core Services:                                              │
│  ┌──────────────────┐  ┌──────────────────┐               │
│  │  Deep Research   │  │  Deep Research   │               │
│  │      API         │  │      Agent       │               │
│  │   (5000)         │  │   (console)      │               │
│  └──────────────────┘  └──────────────────┘               │
│           │                      │                          │
│  Infrastructure:                 │                          │
│  ┌──────────────────┐  ┌────────▼──────┐                 │
│  │  Ollama (LLM)    │  │  Lightning     │                 │
│  │   (11434)        │  │  Server (APO)  │                 │
│  └──────────────────┘  │   (8090)       │                 │
│  ┌──────────────────┐  └───────────────┘                 │
│  │   Crawl4AI       │  ┌──────────────────┐               │
│  │  (11235)         │  │     Redis        │               │
│  └──────────────────┘  │    (6379)        │               │
│  ┌──────────────────┐  └──────────────────┘               │
│  │    SearXNG       │  ┌──────────────────┐               │
│  │    (8080)        │  │     Qdrant       │               │
│  └──────────────────┘  │    (6333)        │               │
│  ┌──────────────────┐  └──────────────────┘               │
│  │    InfluxDB      │  ┌──────────────────┐               │
│  │    (8086)        │  │     Caddy        │               │
│  └──────────────────┘  │   (80/443)       │               │
│                        └──────────────────┘               │
│                                                               │
│  Network: research-network (bridge)                         │
│                                                               │
└─────────────────────────────────────────────────────────────┘
```

### Monitoring Stack (monitoring)

```
┌─────────────────────────────────────────────────┐
│      Monitoring Stack (monitoring)              │
├─────────────────────────────────────────────────┤
│                                                   │
│  ┌──────────────────┐  ┌──────────────────┐   │
│  │  Prometheus      │  │     Grafana      │   │
│  │    (9090)        │  │     (3001)       │   │
│  └──────────────────┘  └──────────────────┘   │
│  ┌──────────────────┐  ┌──────────────────┐   │
│  │  AlertManager    │  │     Jaeger       │   │
│  │    (9093)        │  │    (16686)       │   │
│  └──────────────────┘  └──────────────────┘   │
│  ┌──────────────────────────────────────┐     │
│  │  DeepResearch.Monitoring (8081)     │     │
│  │  Monitoring API (5001)              │     │
│  └──────────────────────────────────────┘     │
│                                                   │
│  Network: monitoring_monitoring (bridge)      │
│                                                   │
└─────────────────────────────────────────────────┘
         Collects metrics and traces from
              DeepResearch stack
```

---

## Quick Start

### Deploy DeepResearch Stack

```bash
# Navigate to project root
cd C:\RepoEx\PhoenixAI\DeepResearch

# Start all services
docker-compose -f Docker/docker-compose.yml up -d

# Check status
docker-compose -f Docker/docker-compose.yml ps

# View logs
docker-compose -f Docker/docker-compose.yml logs -f api
```

**Access Points:**
- API: http://localhost:5000
- Health: http://localhost:5000/health
- Swagger: http://localhost:5000/swagger

### Deploy Monitoring Stack

```bash
# Start monitoring services
docker-compose -f Docker/Observability/docker-compose-monitoring.yml up -d

# Check status
docker-compose -f Docker/Observability/docker-compose-monitoring.yml ps

# View logs
docker-compose -f Docker/Observability/docker-compose-monitoring.yml logs -f prometheus
```

**Access Points:**
- Prometheus: http://localhost:9090
- Grafana: http://localhost:3001 (admin/admin)
- AlertManager: http://localhost:9093
- Jaeger: http://localhost:16686

### Stop Services

```bash
# Stop DeepResearch stack
docker-compose -f Docker/docker-compose.yml down

# Stop Monitoring stack
docker-compose -f Docker/Observability/docker-compose-monitoring.yml down

# Remove volumes (WARNING: Deletes data!)
docker-compose -f Docker/docker-compose.yml down -v
```

---

## Configuration

### Environment Files

#### Docker/.env (DeepResearch Stack)
```ini
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

**For production**: Update all values and secure credentials

#### Docker/Observability/.env (Monitoring Stack)
```ini
JWT_SECRET=your-jwt-secret-key
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
Jwt__SecretKey=your-32-character-minimum-secret-key
Jwt__Issuer=deepresearch-api
Jwt__Audience=deepresearch-api
Jwt__ExpirationMinutes=60
```

**Checkpoint Settings:**
```
Checkpointing__LocalStorageDirectory=/app/data/checkpoints
Checkpointing__MaxCheckpointSizeBytes=52428800
```

**Monitoring:**
```
Monitoring__ServiceUrl=http://deepresearch-monitoring:8081
Monitoring__Enabled=true
```

---

## Docker Compose Files

### Docker/docker-compose.yml (Main Orchestration)

**Purpose**: Complete stack with all services  
**Services**: 11 (API, Agent, Ollama, Crawl4AI, Lightning, Redis, Qdrant, InfluxDB, SearXNG, Caddy, Redis-Exporter)  
**Stack Name**: deepresearch  
**Network**: research-network

**Key Changes Made:**
- Added `name: deepresearch` for consistent stack naming
- Fixed build context paths to use project root (`..`)
- Fixed Dockerfile paths relative to project root
- Created missing .env file with configuration variables

**Build Contexts:**
```
deepresearch-api: context=.. dockerfile=Docker/DeepResearch/Dockerfile
deepresearch-lightning-server: context=.. dockerfile=Docker/lightning-server/Dockerfile
deep-research-agent: context=.. dockerfile=Docker/DeepResearch/Dockerfile
```

### Docker/Observability/docker-compose-monitoring.yml

**Purpose**: Observability and monitoring stack  
**Services**: 6 (Prometheus, Grafana, AlertManager, Jaeger, Monitoring API, Monitoring Service)  
**Stack Name**: monitoring  
**Network**: monitoring_monitoring

**Key Changes Made:**
- Added `name: monitoring` for stack identification
- Changed Grafana port: 3000 → 3001 (conflict with open-webui)
- Changed API port: 5000 → 5001 (conflict with main API)
- Fixed Dockerfile paths
- Disabled OpenTelemetry collector (config issues)
- Fixed Prometheus and AlertManager configurations

**Port Mappings:**
```
Prometheus:    9090
Grafana:       3001 (was 3000)
AlertManager:  9093
Jaeger:        16686
Monitoring API: 5001 (was 5000)
Monitoring:    8081
```

### Docker/docker-compose.api.yml

**Purpose**: API-only deployment for lightweight development  
**Services**: 2 (API, Redis optional)  
**Use Case**: Fast development iteration

```bash
docker-compose -f Docker/docker-compose.api.yml up -d
```

### Docker/docker-compose.dev.yml

**Purpose**: Development overrides for hot reload  
**Use Case**: Combined with main compose file for development

```bash
docker-compose -f Docker/docker-compose.yml -f Docker/docker-compose.dev.yml up
```

### Docker/docker-compose.prod.yml

**Purpose**: Production-optimized configuration  
**Features**:
- Non-root user execution
- Read-only root filesystem
- Enhanced security
- Resource limits
- Persistent volumes

```bash
docker-compose -f Docker/docker-compose.prod.yml up -d
```

---

## Directory Structure

```
Docker/
├── docker-compose.yml                 # Main orchestration (deepresearch stack)
├── docker-compose.api.yml             # API-only configuration
├── docker-compose.dev.yml             # Development overrides
├── docker-compose.prod.yml            # Production configuration
├── .env                               # Configuration variables
├── .dockerignore                      # Build ignore patterns
│
├── DeepResearch/                      # API and Agent services
│   ├── Dockerfile                     # Multi-stage build for API
│   └── ...
│
├── lightning-server/                  # Agent orchestration
│   ├── Dockerfile                     # Python-based Lightning server
│   ├── requirements.txt               # Python dependencies
│   ├── server.py                      # Application entry point
│   └── README.md
│
├── Websearch/                         # Web search and scraping
│   ├── crawl4ai-service/
│   │   ├── Dockerfile
│   │   └── ...
│   ├── crawl4ai_profiles/             # Browser profiles storage
│   ├── searxng/
│   │   ├── settings.yml
│   │   ├── Caddyfile
│   │   └── ...
│   └── README.md
│
├── Observability/                     # Monitoring stack (monitoring)
│   ├── docker-compose-monitoring.yml  # Monitoring orchestration
│   ├── .env                           # Monitoring configuration
│   ├── prometheus.yml                 # Prometheus config
│   ├── alerts.yml                     # Alert rules
│   ├── alertmanager-config.yml        # AlertManager routing
│   ├── grafana-dashboard.json         # Dashboard definition
│   ├── grafana-datasource.yml         # Datasource provisioning
│   ├── otel-collector-config.yml      # OpenTelemetry config
│   └── README.md
│
└── data/                              # Persistent data (runtime created)
    ├── checkpoints/                   # Workflow checkpoints
    ├── keys/                          # Data protection keys
    └── logs/                          # Application logs
```

---

## Deployment Guides

### Development Deployment

```bash
# Build and start (with live reload)
docker-compose -f Docker/docker-compose.yml -f Docker/docker-compose.dev.yml up

# API only (faster startup)
docker-compose -f Docker/docker-compose.api.yml up -d

# Check services
docker-compose -f Docker/docker-compose.api.yml ps
```

**Access:**
- API: http://localhost:5000
- Swagger: http://localhost:5000/swagger

### Staging Deployment

```bash
# Start production stack
docker-compose -f Docker/docker-compose.prod.yml up -d

# Start monitoring
docker-compose -f Docker/Observability/docker-compose-monitoring.yml up -d

# Verify all services
docker-compose -f Docker/docker-compose.prod.yml ps
docker-compose -f Docker/Observability/docker-compose-monitoring.yml ps
```

**Important**: Update .env files with production values before deploying

### Production Deployment

1. **Prepare environment:**
   ```bash
   # Create .env with production secrets
   export JWT_SECRET_KEY="your-strong-secret-key-minimum-32-chars"
   export INFLUXDB_PASSWORD="strong-password"
   export QDRANT_API_KEY="strong-key"
   
   # Create environment file
   cat > Docker/.env << EOF
   JWT_SECRET_KEY=$JWT_SECRET_KEY
   INFLUXDB_PASSWORD=$INFLUXDB_PASSWORD
   QDRANT_API_KEY=$QDRANT_API_KEY
   # ... other variables
   EOF
   ```

2. **Deploy stacks:**
   ```bash
   # Deploy main stack
   docker-compose -f Docker/docker-compose.prod.yml up -d
   
   # Deploy monitoring
   docker-compose -f Docker/Observability/docker-compose-monitoring.yml up -d
   
   # Enable auto-restart
   docker-compose -f Docker/docker-compose.prod.yml restart --timeout 10
   ```

3. **Verify deployment:**
   ```bash
   # Check all services
   docker-compose -f Docker/docker-compose.prod.yml ps
   
   # Test API
   curl -s http://localhost:5000/health | jq .
   
   # Test monitoring
   curl -s http://localhost:9090/-/healthy
   ```

---

## Service Details

### DeepResearch Stack (deepresearch)

| Service | Image | Port | Purpose |
|---------|-------|------|---------|
| API | deepresearch-api | 5000 | HTTP API endpoint |
| Agent | deep-research-agent | - | Console workflow interface |
| Ollama | ollama:latest | 11434 | Local LLM inference |
| Crawl4AI | unclecode/crawl4ai | 11235 | Web scraping service |
| Lightning | deepresearch-lightning-server | 8090 | Agent orchestration (APO/VERL) |
| Redis | redis:7-alpine | 6379 | Distributed cache |
| Qdrant | qdrant:latest | 6333 | Vector database |
| InfluxDB | influxdb:2.7 | 8086 | Time-series metrics |
| SearXNG | searxng:latest | 8080 | Meta search engine |
| Caddy | caddy:2-alpine | 80/443 | Reverse proxy/TLS |
| Redis-Exporter | redis_exporter | 9121 | Prometheus metrics |

### Monitoring Stack (monitoring)

| Service | Image | Port | Purpose |
|---------|-------|------|---------|
| Prometheus | prom/prometheus | 9090 | Metrics collection |
| Grafana | grafana:latest | 3001 | Dashboard visualization |
| AlertManager | prom/alertmanager | 9093 | Alert routing |
| Jaeger | jaeger:all-in-one | 16686 | Distributed tracing |
| API | monitoring-deepresearch-api | 5001 | Monitoring API |
| Service | monitoring-deepresearch-monitoring | 8081 | Custom monitoring |

---

## Troubleshooting

### Services Won't Start

```bash
# Check logs
docker-compose -f Docker/docker-compose.yml logs [service]

# View full error with tail
docker-compose -f Docker/docker-compose.yml logs --tail=100 [service]

# Restart service
docker-compose -f Docker/docker-compose.yml restart [service]
```

### Port Already in Use

**Symptoms**: "Bind for 0.0.0.0:[port] failed: port is already allocated"

**Solution:**
1. Identify service using port: `netstat -ano | findstr :[port]`
2. Either:
   - Stop conflicting service/container
   - Change port in docker-compose.yml
   - Use different compose file

### Health Checks Failing

**Symptoms**: Services marked "unhealthy" but working

**Causes:**
- Health check endpoint not implemented
- Health check requires tool not in image (e.g., curl)
- Network connectivity issues

**Solutions:**
- Verify `/health` endpoint exists
- Add required tools to Dockerfile
- Check network connectivity: `docker exec [container] curl http://service:port/health`

### Volume Mount Issues

**Symptoms**: "Error creating mount point"

**Solutions:**
```bash
# Create data directories
mkdir -p Docker/data/checkpoints
mkdir -p Docker/data/keys
mkdir -p Docker/logs

# Set permissions
chmod 755 Docker/data
chmod 755 Docker/logs
```

### Configuration Not Applying

**Symptoms**: Environment variables not taking effect

**Solutions:**
1. Verify .env file syntax
2. Check variable names exactly match (case-sensitive)
3. Rebuild image after config changes: `docker-compose build --no-cache`
4. Remove old containers: `docker-compose down && docker-compose up -d`

### Memory Issues

**Symptoms**: "OOMKilled" container status, slow performance

**Solutions:**
```bash
# Check Docker memory allocation
docker stats

# Increase Docker Desktop memory (GUI or settings)
# Or limit service resources in docker-compose.yml:
services:
  api:
    deploy:
      resources:
        limits:
          memory: 2G
        reservations:
          memory: 1G
```

---

## Database and Persistence

### Volumes

**DeepResearch Stack:**
```
- ollama_data: Ollama models and cache
- redis-data: Redis persistence
- qdrant_storage: Vector database
- lightning-data: Lightning server data
- prometheus-data: Prometheus metrics
- influxdb-data: InfluxDB time-series
- grafana-data: Grafana dashboards
```

**Data Directory:**
```
data/
├── checkpoints/      # Workflow checkpoints
├── keys/             # Data protection keys
└── logs/             # Application logs
```

### Backup

```bash
# Backup all volumes
docker run --rm -v deepresearch_prometheus-data:/data \
  -v $(pwd)/backup:/backup \
  alpine tar czf /backup/prometheus.tar.gz -C /data .

# Backup specific folder
tar czf backup-checkpoints.tar.gz Docker/data/checkpoints/
```

---

## Security Considerations

### Production Checklist

- [ ] Change all default passwords (Grafana, InfluxDB, etc.)
- [ ] Use strong JWT secrets (32+ characters)
- [ ] Store secrets in environment variables, not code
- [ ] Use read-only root filesystems
- [ ] Run services with non-root users
- [ ] Enable HTTPS/TLS (configure Caddy)
- [ ] Set resource limits
- [ ] Configure network isolation
- [ ] Enable container restart policies
- [ ] Monitor resource usage
- [ ] Regular backups of persistent data
- [ ] Update base images regularly

### Secrets Management

```bash
# Use .env files (never commit to git)
echo "Docker/.env" >> .gitignore

# Or use Docker secrets (in swarm mode)
# Or use environment variable substitution

# Example with secure variable
export JWT_SECRET=$(openssl rand -base64 32)
docker-compose -f Docker/docker-compose.yml up -d
```

---

## Maintenance

### Log Rotation

Docker compose default: 10M per file, 3 files max

Configured in docker-compose.yml:
```yaml
logging:
  driver: "json-file"
  options:
    max-size: "10m"
    max-file: "3"
```

### Clean Up

```bash
# Remove stopped containers
docker container prune -f

# Remove unused images
docker image prune -f

# Remove unused volumes
docker volume prune -f

# Full cleanup (WARNING: removes untagged images)
docker system prune -a --volumes -f
```

### Updates

```bash
# Pull latest images
docker-compose -f Docker/docker-compose.yml pull

# Rebuild images from source
docker-compose -f Docker/docker-compose.yml build --no-cache

# Restart services with new images
docker-compose -f Docker/docker-compose.yml up -d
```

---

## References

- **Docker Docs**: https://docs.docker.com/
- **Docker Compose Docs**: https://docs.docker.com/compose/
- **Docker Best Practices**: https://docs.docker.com/develop/dev-best-practices/
- **Dockerfile Reference**: https://docs.docker.com/engine/reference/builder/

---

**Last Updated**: 2026-02-20  
**Status**: ✅ Complete  
**Version**: 2.0 (Updated with monitoring stack and current deployment status)
