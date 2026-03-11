# OpenTelemetry & Monitoring Setup Guide

## 📊 Overview

This guide covers the complete observability stack for Lightning Server, including:

- **OpenTelemetry** - Distributed tracing for request flows
- **Prometheus** - Metrics collection and aggregation
- **Grafana** - Visualization dashboards
- **Alerting** - Prometheus alerting rules for critical conditions

---

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Lightning Server                        │
│  ┌────────────────┐  ┌─────────────────┐  ┌──────────────┐ │
│  │ FastAPI App    │  │ Lightning Store │  │ MongoDB      │ │
│  │ (OpenTelemetry)│─→│ (OTLP Endpoint) │←─│ (Persistence)│ │
│  └────────────────┘  └─────────────────┘  └──────────────┘ │
│         ↓ traces              ↓ spans             ↓        │
└─────────┼─────────────────────┼──────────────────┼─────────┘
          ↓                     ↓                  ↓
    ┌─────────────┐      ┌──────────────┐   ┌────────────┐
    │ OTLP Export │      │ Prometheus   │   │ MongoDB    │
    │ (HTTP)      │      │ Metrics:9091 │   │ Metrics    │
    └─────────────┘      └──────────────┘   └────────────┘
          ↓                     ↓                  ↓
    ┌─────────────────────────────────────────────────────┐
    │                   Grafana Dashboards                 │
    │  • Request traces  • Metrics  • Alerts  • Logs      │
    └─────────────────────────────────────────────────────┘
```

---

## 🚀 Quick Start

### Start with Monitoring Stack

```bash
# Start Lightning Server + MongoDB + Monitoring (Prometheus + Grafana)
docker-compose --profile monitoring up -d

# Access monitoring services:
# - Lightning Server API: http://localhost:9090
# - Prometheus: http://localhost:9092
# - Grafana: http://localhost:3000 (admin/admin)
# - Prometheus Metrics Endpoint: http://localhost:9091/metrics
```

### Verify OpenTelemetry Setup

```bash
# Check health endpoint for OpenTelemetry status
curl http://localhost:9090/health | jq '.opentelemetry'

# Expected output:
{
  "enabled": true,
  "otlpEndpoint": "http://lightning-server:9090/v1/traces"
}
```

### View Traces

1. **Lightning Store Dashboard** (built-in):
   - Navigate to http://localhost:9090/dashboard
   - View rollouts, attempts, and spans with distributed traces

2. **Grafana** (comprehensive):
   - Navigate to http://localhost:3000
   - Login: admin/admin
   - Navigate to "Lightning Server - Overview" dashboard

---

## 🔧 Configuration

### Environment Variables

All OpenTelemetry configuration is managed via environment variables:

```bash
# Enable/disable OpenTelemetry
OTEL_ENABLED=true

# OTLP endpoint (Lightning Store /v1/traces endpoint)
OTEL_OTLP_ENDPOINT=http://lightning-server:9090/v1/traces

# Export timeout in seconds
OTEL_EXPORT_TIMEOUT=30

# Enable console export for debugging (prints traces to stdout)
OTEL_CONSOLE_EXPORT=false

# Service name for traces
OTEL_SERVICE_NAME=lightning-server

# Environment label (development/staging/production)
ENVIRONMENT=development
```

### Grafana Provisioning

Grafana is auto-configured with:

1. **Datasources** (`grafana-provisioning/datasources/datasources.yml`):
   - Prometheus (metrics from port 9091)
   - Lightning Traces (OTLP traces via Tempo-compatible endpoint)

2. **Dashboards** (`grafana-provisioning/dashboards/`):
   - `lightning-server-overview.json` - Main monitoring dashboard
   - Auto-loaded on Grafana startup

3. **Custom Dashboards**:
   - Place JSON files in `grafana-provisioning/dashboards/`
   - They will auto-load on restart

---

## 📈 Metrics & Traces

### Available Metrics

Lightning Server exposes Prometheus metrics on port **9091**:

```bash
curl http://localhost:9091/metrics
```

**Key Metrics:**

| Metric Name | Type | Description |
|-------------|------|-------------|
| `http_requests_total` | Counter | Total HTTP requests by method, handler, status |
| `http_request_duration_seconds` | Histogram | Request latency distribution |
| `lightning_rollouts_total` | Counter | Rollouts by status (queuing, running, succeeded, failed) |
| `lightning_attempts_total` | Counter | Attempts by status |
| `lightning_spans_total` | Counter | Spans by kind (llm, tool, agent, etc.) |
| `process_cpu_seconds_total` | Counter | CPU usage |
| `process_resident_memory_bytes` | Gauge | Memory usage |

### Distributed Tracing

OpenTelemetry automatically instruments:

- **FastAPI endpoints** - All HTTP requests traced
- **httpx client** - Outbound HTTP calls traced
- **Agent-Lightning operations** - Rollouts, attempts, spans stored with trace context

**Trace Flow Example:**

```
POST /api/tasks/submit
  ├─ lightning_store.enqueue_rollout()
  │   ├─ MongoDB write (rollout document)
  │   └─ Span creation
  ├─ Agent execution
  │   ├─ LLM call (traced via httpx)
  │   └─ Tool invocations
  └─ Response returned
```

Each span includes:
- **Span ID** - Unique identifier
- **Trace ID** - Groups related spans
- **Parent Span ID** - Hierarchical relationships
- **Attributes** - Custom metadata (agent_id, task_id, etc.)
- **Duration** - Execution time

---

## 🔔 Alerting

### Prometheus Alert Rules

Alert rules are defined in `prometheus-alerts.yml`:

#### 🚨 Critical Alerts

1. **LightningServerDown**
   - Lightning Server unreachable for > 1 minute
   - **Action**: Check container logs, restart if necessary

2. **HighErrorRate**
   - HTTP 5xx errors > 5% of total requests
   - **Action**: Investigate error logs, check MongoDB connection

3. **MongoDBConnectionIssues**
   - Cannot connect to MongoDB for > 2 minutes
   - **Action**: Verify MongoDB replica set status

#### ⚠️ Warning Alerts

1. **HighRequestLatency**
   - p95 latency > 1 second for 5 minutes
   - **Action**: Check for slow MongoDB queries, optimize indexes

2. **HighRolloutQueueDepth**
   - >100 rollouts queued for 10 minutes
   - **Action**: Scale up workers, check for processing bottlenecks

3. **RolloutFailureRateHigh**
   - Rollout failure rate > 10%
   - **Action**: Investigate rollout logs, check LLM connectivity

4. **HighMemoryUsage**
   - Memory usage > 4GB for 5 minutes
   - **Action**: Check for memory leaks, increase container limits

5. **HighSpanDropRate**
   - Spans being dropped (export issues)
   - **Action**: Check OTLP endpoint connectivity

### Viewing Alerts

**Prometheus:**
```
http://localhost:9092/alerts
```

**Grafana:**
- Navigate to "Alerting" → "Alert Rules"
- Configure notification channels (email, Slack, PagerDuty)

---

## 📊 Grafana Dashboards

### Lightning Server Overview

**URL**: http://localhost:3000/d/lightning-server-overview

**Panels:**

1. **Request Rate** - Requests/second by method and endpoint
2. **Request Latency (p95)** - 95th percentile response time
3. **Rollouts by Status** - Time-series of rollout states
4. **Spans by Kind** - Distribution of span types (LLM, tool, agent)
5. **CPU Usage** - Process CPU consumption
6. **Memory Usage** - Resident memory in bytes

### Creating Custom Dashboards

```json
// Example: Custom panel for task submission rate
{
  "targets": [
    {
      "expr": "rate(http_requests_total{handler=\"/api/tasks/submit\"}[5m])",
      "legendFormat": "Submit Rate"
    }
  ],
  "title": "Task Submission Rate",
  "type": "graph"
}
```

Save as JSON in `grafana-provisioning/dashboards/` and restart Grafana.

---

## 🧪 Testing & Debugging

### Generate Test Traffic

```bash
# Submit test tasks
for i in {1..10}; do
  curl -X POST http://localhost:9090/api/tasks/submit \
    -H "Content-Type: application/json" \
    -d '{
      "agentId": "test-agent-1",
      "task": {
        "id": "test-'$i'",
        "name": "Test Task '$i'",
        "description": "Test rollout for monitoring",
        "input": {"prompt": "Test prompt"}
      }
    }'
done
```

### View Traces in Console (Debug Mode)

```bash
# Enable console export
export OTEL_CONSOLE_EXPORT=true

# Restart server
docker-compose restart lightning-server

# Watch logs for traces
docker logs -f lightning-server
```

### Query Metrics

```bash
# Get current rollout counts
curl -s http://localhost:9091/metrics | grep lightning_rollouts_total

# Get request rate (last 5 minutes)
curl -s 'http://localhost:9092/api/v1/query?query=rate(http_requests_total[5m])' | jq
```

---

## 🔍 Troubleshooting

### Issue: No traces appearing

**Diagnosis:**
```bash
# Check OTLP endpoint
curl http://localhost:9090/v1/traces

# Check OpenTelemetry config
curl http://localhost:9090/health | jq '.opentelemetry'

# Check server logs
docker logs lightning-server | grep -i "opentelemetry\|otlp"
```

**Solutions:**
- Verify `OTEL_ENABLED=true` in environment
- Check OTLP endpoint is reachable (default: `http://lightning-server:9090/v1/traces`)
- Ensure Agent-Lightning LightningStoreServer is initialized

### Issue: Prometheus not scraping metrics

**Diagnosis:**
```bash
# Check Prometheus targets
curl http://localhost:9092/api/v1/targets | jq '.data.activeTargets[] | {job, health}'

# Check metrics endpoint
curl http://localhost:9091/metrics
```

**Solutions:**
- Verify `lightning-server:9091` is reachable from Prometheus container
- Check `prometheus.yml` scrape config
- Ensure `PROMETHEUS_METRICS_ENABLED=true`

### Issue: Grafana dashboards not loading

**Diagnosis:**
```bash
# Check Grafana datasource status
curl http://admin:admin@localhost:3000/api/datasources | jq

# Check dashboard provisioning logs
docker logs lightning-grafana | grep -i provision
```

**Solutions:**
- Verify `grafana-provisioning/` directory is mounted correctly
- Check datasource URLs (Prometheus: `http://prometheus:9090`)
- Restart Grafana: `docker-compose restart grafana`

### Issue: High span drop rate

**Diagnosis:**
```bash
# Check OTLP export errors
docker logs lightning-server | grep -i "otlp\|exporter"

# Check export timeout
curl http://localhost:9090/health | jq '.opentelemetry.otlpEndpoint'
```

**Solutions:**
- Increase `OTEL_EXPORT_TIMEOUT` (default: 30s)
- Check Lightning Store /v1/traces endpoint connectivity
- Reduce trace sampling rate if volume is too high

---

## 🔐 Security Considerations

### Production Deployment

1. **Authentication:**
   ```yaml
   # Grafana: Change default password
   - GF_SECURITY_ADMIN_PASSWORD=${GRAFANA_PASSWORD}
   
   # Prometheus: Add basic auth
   basic_auth:
     username: admin
     password_file: /etc/prometheus/password
   ```

2. **Network Isolation:**
   - Expose only necessary ports (9090, 3000)
   - Use internal Docker networks for metrics scraping
   - Consider VPN/firewall for monitoring access

3. **TLS/SSL:**
   ```yaml
   # Enable HTTPS for OTLP endpoint
   OTEL_OTLP_ENDPOINT=https://lightning-server:9090/v1/traces
   OTEL_OTLP_HEADERS=Authorization=Bearer ${API_TOKEN}
   ```

---

## 📚 Additional Resources

### OpenTelemetry Documentation
- [OpenTelemetry Python](https://opentelemetry.io/docs/languages/python/)
- [FastAPI Instrumentation](https://opentelemetry-python-contrib.readthedocs.io/en/latest/instrumentation/fastapi/fastapi.html)
- [OTLP Specification](https://opentelemetry.io/docs/specs/otlp/)

### Prometheus Documentation
- [Prometheus Querying](https://prometheus.io/docs/prometheus/latest/querying/basics/)
- [Alerting Rules](https://prometheus.io/docs/prometheus/latest/configuration/alerting_rules/)
- [Recording Rules](https://prometheus.io/docs/prometheus/latest/configuration/recording_rules/)

### Grafana Documentation
- [Dashboard Provisioning](https://grafana.com/docs/grafana/latest/administration/provisioning/)
- [Datasource Configuration](https://grafana.com/docs/grafana/latest/datasources/)
- [Alert Notifications](https://grafana.com/docs/grafana/latest/alerting/notifications/)

### Agent-Lightning Specific
- [LightningStore Metrics](https://github.com/microsoft/agent-lightning/blob/main/docs/metrics.md)
- [OTLP Traces Endpoint](https://github.com/microsoft/agent-lightning/blob/main/docs/telemetry.md)

---

## 📝 Summary Checklist

✅ **OpenTelemetry Configured**
- [x] FastAPI instrumentation enabled
- [x] httpx client instrumentation enabled
- [x] OTLP exporter configured
- [x] Traces exported to Lightning Store /v1/traces

✅ **Prometheus Configured**
- [x] Metrics endpoint exposed on port 9091
- [x] Scrape config defined in `prometheus.yml`
- [x] Alert rules defined in `prometheus-alerts.yml`
- [x] Prometheus container running

✅ **Grafana Configured**
- [x] Datasources auto-provisioned (Prometheus, Lightning Traces)
- [x] Dashboards auto-loaded from `grafana-provisioning/dashboards/`
- [x] Grafana accessible on port 3000

✅ **Docker Orchestration**
- [x] Monitoring services in `docker-compose.yml` (monitoring profile)
- [x] Environment variables configured
- [x] Volumes mounted for persistent data

✅ **Documentation**
- [x] Monitoring setup guide (this document)
- [x] Environment configuration documented (`.env.example`)
- [x] Troubleshooting guide included

---

**Next Steps:**
- Run `docker-compose --profile monitoring up -d` to start the stack
- Access Grafana at http://localhost:3000 (admin/admin)
- View "Lightning Server - Overview" dashboard
- Submit test tasks to generate traces and metrics
- Configure alerting notifications (email, Slack, PagerDuty)
