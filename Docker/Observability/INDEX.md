# DeepResearch Observability - File Index

## 📖 Documentation (Start Here)

| File | Purpose | When to Read |
|------|---------|--------------|
| **[SETUP-COMPLETE.md](./SETUP-COMPLETE.md)** | ⭐ Quick overview & verification | **Start here!** |
| **[PORT-CONFIGURATION.md](./PORT-CONFIGURATION.md)** | Port assignments and changes | Port conflicts or access issues |
| **[README.md](./README.md)** | Directory reference guide | Quick reference |
| **[OBSERVABILITY.md](./OBSERVABILITY.md)** | Complete implementation guide | Detailed usage |
| **[OBSERVABILITY-QUICK-START.md](./OBSERVABILITY-QUICK-START.md)** | Command cheat sheet | Daily operations |
| **[IMPLEMENTATION-SUMMARY.md](./IMPLEMENTATION-SUMMARY.md)** | What was implemented | Understanding the setup |
| **[MONITORING.md](./MONITORING.md)** | Legacy monitoring docs | Historical reference |

## 🚀 Quick Start Files

| File | Purpose |
|------|---------|
| **[start-observability.ps1](./start-observability.ps1)** | ⭐ Start all services |
| **[stop-observability.ps1](./stop-observability.ps1)** | Stop services (interactive) |
| **[docker-compose-monitoring.yml](./docker-compose-monitoring.yml)** | Main stack definition |

## ⚙️ Configuration Files

### Main Configuration (in `config/` directory)

| File | Purpose | Edit When |
|------|---------|-----------|
| **config/prometheus.yml** | Metrics scraping | Adding new services |
| **config/alertmanager.yml** | Alert routing | Changing notifications |
| **config/otel-collector-config.yml** | OpenTelemetry collector | Advanced telemetry |
| **config/rules/deepresearch-alerts.yml** | Alert definitions | Creating new alerts |

### Grafana Configuration

| File | Purpose |
|------|---------|
| **config/grafana/datasources/datasources.yml** | Prometheus & Jaeger datasources |
| **config/grafana/dashboards/dashboard-provider.yml** | Dashboard provisioning |
| **config/grafana/dashboards/masterworkflow-dashboard.json** | MasterWorkflow metrics dashboard |

### Legacy Files (retained for compatibility)

| File | Status | Notes |
|------|--------|-------|
| prometheus.yml | Deprecated | Use `config/prometheus.yml` |
| alertmanager-config.yml | Deprecated | Use `config/alertmanager.yml` |
| otel-collector-config.yml | Deprecated | Use `config/otel-collector-config.yml` |
| alerts.yml | Deprecated | Use `config/rules/deepresearch-alerts.yml` |
| grafana-dashboard.json | Deprecated | Use `config/grafana/dashboards/` |
| grafana-datasource.yml | Deprecated | Use `config/grafana/datasources/` |

## 📁 Directory Structure

```
Docker/Observability/
│
├── 📚 Documentation
│   ├── SETUP-COMPLETE.md              ⭐ Start here
│   ├── README.md                      Quick reference
│   ├── OBSERVABILITY.md               Complete guide
│   ├── OBSERVABILITY-QUICK-START.md   Command reference
│   ├── IMPLEMENTATION-SUMMARY.md      Implementation details
│   ├── MONITORING.md                  Legacy docs
│   └── INDEX.md                       This file
│
├── 🚀 Quick Start Scripts
│   ├── start-observability.ps1        ⭐ Start stack
│   ├── stop-observability.ps1         Stop stack
│   └── docker-compose-monitoring.yml  Stack definition
│
├── ⚙️ config/                         Active configurations
│   ├── prometheus.yml
│   ├── alertmanager.yml
│   ├── otel-collector-config.yml
│   ├── rules/
│   │   └── deepresearch-alerts.yml
│   └── grafana/
│       ├── dashboards/
│       │   ├── dashboard-provider.yml
│       │   └── masterworkflow-dashboard.json
│       └── datasources/
│           └── datasources.yml
│
└── 📦 Legacy Files (deprecated)
    ├── prometheus.yml
    ├── alertmanager-config.yml
    ├── otel-collector-config.yml
    ├── alerts.yml
    ├── grafana-dashboard.json
    └── grafana-datasource.yml
```

## 🎯 Quick Actions

### I want to...

**Start monitoring** → Run `.\start-observability.ps1`

**View metrics** → Open http://localhost:3001 (Grafana)

**View traces** → Open http://localhost:16686 (Jaeger)

**Stop services** → Run `.\stop-observability.ps1`

**Add new service to monitor** → Edit `config/prometheus.yml`

**Create new alert** → Edit `config/rules/deepresearch-alerts.yml`

**Add new dashboard** → Place JSON in `config/grafana/dashboards/`

**Check logs** → Run `docker-compose -f docker-compose-monitoring.yml logs -f`

**Understand the setup** → Read [SETUP-COMPLETE.md](./SETUP-COMPLETE.md)

## 🔗 Related Files (Outside This Directory)

### Application Code
```
DeepResearchAgent/Observability/
├── DiagnosticConfig.cs         # Metrics & tracing definitions
├── TelemetryExtensions.cs      # DI registration
├── ActivityScope.cs            # Tracing helper
└── MetricsCollector.cs         # Execution tracking
```

### Application Configuration
```
DeepResearchAgent/appsettings.json
└── OpenTelemetry section       # Application telemetry config
```

## 📊 What Each Service Does

| Service | Port | Purpose | Access |
|---------|------|---------|--------|
| **Jaeger** | 16686 | Distributed tracing UI | http://localhost:16686 |
| **Prometheus** | 9090 | Metrics storage & queries | http://localhost:9090 |
| **Grafana** | 3001 | Dashboards & visualization | http://localhost:3001 |
| **AlertManager** | 9093 | Alert routing & management | http://localhost:9093 |
| **OTel Collector** | 4317/4318 | Telemetry collection | - |

## 🆘 Troubleshooting

**Services won't start** → Check Docker is running, ports aren't in use

**Metrics not showing** → Check http://localhost:9090/targets in Prometheus

**Traces not appearing** → Verify app OTLP endpoint: http://localhost:4317

**Dashboard blank** → Check datasource in Grafana settings

**Need help?** → See [OBSERVABILITY.md](./OBSERVABILITY.md#troubleshooting)

---

**Last Updated**: 2026-03-12  
**Location**: `C:\RepoEx\PhoenixAI\DeepResearch\Docker\Observability\`  
**Quick Start**: Run `.\start-observability.ps1`
