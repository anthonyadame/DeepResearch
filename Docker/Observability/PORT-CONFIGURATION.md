# Port Configuration Note

## Grafana Port Change

**Port**: Changed from `3000` to `3001`  
**Reason**: Port 3000 is already in use by `open-webui`  
**Date**: 2026-03-12

## Updated Access URL

- **Old**: http://localhost:3000
- **New**: http://localhost:3001

## Services and Ports

| Service | Port | Notes |
|---------|------|-------|
| **Grafana** | **3001** | Changed from 3000 to avoid conflict |
| Jaeger UI | 16686 | No change |
| Jaeger OTLP gRPC | 4317 | No change |
| Jaeger OTLP HTTP | 4318 | No change |
| Prometheus | 9090 | No change |
| AlertManager | 9093 | No change |
| OTel Collector | 8888/8889 | No change |

## Files Updated

✅ `docker-compose-monitoring.yml` - Port mapping changed to `3001:3000`  
✅ `docker-compose-monitoring.yml` - GF_SERVER_ROOT_URL updated to port 3001  
✅ `README.md` - All Grafana URL references updated  
✅ `SETUP-COMPLETE.md` - Access URL updated  
✅ `OBSERVABILITY.md` - Access URL updated  
✅ `OBSERVABILITY-QUICK-START.md` - Access URL updated  
✅ `IMPLEMENTATION-SUMMARY.md` - Access URL updated  
✅ `INDEX.md` - Port table and quick actions updated  
✅ `REORGANIZATION-SUMMARY.md` - Access URL updated  
✅ `start-observability.ps1` - URL references and browser launch updated  

## Quick Start

```powershell
cd C:\RepoEx\PhoenixAI\DeepResearch\Docker\Observability
.\start-observability.ps1
```

Then access Grafana at: **http://localhost:3001**

## Login Credentials

- **Username**: admin
- **Password**: admin

---

**Note**: All documentation has been updated to reflect the new port. The old port 3000 is no longer used to avoid conflicts with open-webui running on the same host.
