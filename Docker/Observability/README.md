# DeepResearch Observability Stack

Complete observability solution for DeepResearch using OpenTelemetry, Prometheus, Grafana, Jaeger, and AlertManager.

## 📁 Directory Structure

```
Docker/Observability/
├── config/
│   ├── rules/
│   │   └── deepresearch-alerts.yml      # Prometheus alerting rules
│   ├── grafana/
│   │   ├── dashboards/
│   │   │   ├── dashboard-provider.yml   # Dashboard provisioning
│   │   │   └── masterworkflow-dashboard.json  # MasterWorkflow metrics
│   │   └── datasources/
│   │       └── datasources.yml          # Prometheus & Jaeger datasources
│   ├── prometheus.yml                   # Prometheus scraping configuration
│   ├── alertmanager.yml                 # AlertManager routing
│   └── otel-collector-config.yml        # OpenTelemetry Collector config
├── docker-compose-monitoring.yml        # Main observability stack
├── start-observability.ps1              # Quick start script
├── stop-observability.ps1               # Shutdown script
├── OBSERVABILITY.md                     # Complete guide
├── OBSERVABILITY-QUICK-START.md         # Command reference
└── IMPLEMENTATION-SUMMARY.md            # Implementation overview
```

## 🚀 Quick Start

### 1. Start the Observability Stack

```powershell
# From this directory
.\start-observability.ps1

# Or manually
docker-compose -f docker-compose-monitoring.yml up -d
```

### 2. Access Dashboards

- **Grafana**: http://localhost:3001 (admin/admin)
  - Pre-configured with MasterWorkflow Observability dashboard

- **Jaeger**: http://localhost:16686
  - Distributed tracing UI
  - Search traces by service, operation, duration

- **Prometheus**: http://localhost:9090
  - Direct metric queries
  - View targets and alerts

- **AlertManager**: http://localhost:9093
  - Alert management and routing

### 3. Configure Your Application

Update your `appsettings.json`:

```json
{
  "OpenTelemetry": {
    "ServiceName": "DeepResearchAgent",
    "Exporters": {
      "Otlp": {
        "Endpoint": "http://localhost:4317"
      }
    }
  }
}
```

### 4. Run Your Application

```powershell
# DeepResearchAgent will automatically export telemetry
dotnet run --project ../../DeepResearchAgent
```

## 📊 What's Monitored

### MasterWorkflow Metrics
- **Step execution duration** (p50, p95, p99)
- **Workflow total duration**
- **Error rates and types**
- **Success/failure counts**

### LLM Performance
- **Request duration**
- **Token usage**
- **Provider latency**

### Tool Invocations
- **Success/failure rates**
- **Duration per tool**
- **Invocation frequency**

### System Metrics
- **CPU and memory usage**
- **Cache hit rates**
- **State operations**

## 🔔 Pre-Configured Alerts

| Alert | Threshold | Action |
|-------|-----------|--------|
| HighWorkflowErrorRate | >0.1 errors/sec | Investigate immediately |
| SlowWorkflowExecution | P95 > 5 min | Check LLM performance |
| LLMServiceDegradation | P95 > 30 sec | Verify provider status |
| LowCacheHitRate | <50% | Review caching strategy |

## 🛠️ Configuration

### Prometheus Scraping

Edit `config/prometheus.yml` to add new targets:

```yaml
scrape_configs:
  - job_name: 'my-service'
    static_configs:
      - targets: ['my-service:8080']
    metrics_path: '/metrics'
```

### Alert Rules

Add custom alerts in `config/rules/deepresearch-alerts.yml`:

```yaml
- alert: MyCustomAlert
  expr: my_metric > threshold
  for: 5m
  labels:
    severity: warning
  annotations:
    summary: "Custom alert triggered"
```

### Grafana Dashboards

Add new dashboards in `config/grafana/dashboards/` as JSON files.

## 🔧 Maintenance

### View Logs

```powershell
# All services
docker-compose -f docker-compose-monitoring.yml logs -f

# Specific service
docker-compose -f docker-compose-monitoring.yml logs -f prometheus
docker-compose -f docker-compose-monitoring.yml logs -f jaeger
```

### Restart Services

```powershell
docker-compose -f docker-compose-monitoring.yml restart
```

### Stop Services

```powershell
# Using script (interactive)
.\stop-observability.ps1

# Or manually (preserve data)
docker-compose -f docker-compose-monitoring.yml down

# Stop and remove all data
docker-compose -f docker-compose-monitoring.yml down -v
```

## 🐛 Troubleshooting

### Metrics Not Showing

1. Check Prometheus targets: http://localhost:9090/targets
2. Verify your app exposes `/metrics` endpoint
3. Ensure `host.docker.internal` resolves correctly

### Traces Not Appearing

1. Check OTLP endpoint is accessible: http://localhost:4317
2. Verify `OpenTelemetry:Exporters:Otlp:Endpoint` in appsettings.json
3. Check Jaeger collector logs:
   ```powershell
   docker logs deepresearch-jaeger
   ```

### Dashboard Not Loading

1. Verify datasource configuration in Grafana
2. Check Prometheus is running: http://localhost:9090
3. Review Grafana logs:
   ```powershell
   docker logs deepresearch-grafana
   ```

## 📚 Documentation

- **OBSERVABILITY.md** - Complete guide with examples and best practices
- **OBSERVABILITY-QUICK-START.md** - Command reference cheat sheet
- **IMPLEMENTATION-SUMMARY.md** - Implementation details and overview

## 🌐 Network Configuration

All services run on the `deepresearch-hub` network to communicate with other DeepResearch stacks:
- Core Stack (Redis, InfluxDB)
- AI Stack (Qdrant, LLMs)
- WebSearch Stack (SearXNG, Crawl4AI)

### Ports

| Service | Port | Purpose |
|---------|------|---------|
| Grafana | 3001 | Web UI (changed from 3000 to avoid conflict with open-webui) |
| Jaeger UI | 16686 | Tracing UI |
| Jaeger OTLP gRPC | 4317 | Telemetry ingestion |
| Jaeger OTLP HTTP | 4318 | Telemetry ingestion |
| Prometheus | 9090 | Metrics & queries |
| AlertManager | 9093 | Alert management |
| OTel Collector | 8888/8889 | Metrics export |

## 🔐 Security

Default credentials (change in production):
- **Grafana**: admin / admin
- **Prometheus**: No authentication (configure in production)
- **AlertManager**: No authentication (configure in production)

## 💡 Tips

1. **Use Tags**: Add meaningful tags to activities for better filtering
2. **Monitor P95/P99**: Focus on high percentiles, not just averages
3. **Set Realistic Alerts**: Tune thresholds based on actual performance
4. **Regular Reviews**: Check dashboards weekly to spot trends
5. **Trace Sampling**: Consider sampling in production to reduce overhead

---

**For detailed usage instructions, see [OBSERVABILITY.md](./OBSERVABILITY.md)**
