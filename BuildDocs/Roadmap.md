# Roadmap

**Last Updated**: 2026-02-20  
**Status**: Active Development with Infrastructure Established

## Objectives

- ✅ Capture current capabilities implemented in the codebase.
- ✅ Prioritize stabilization, documentation, and test coverage for existing features.
- 🔄 Plan follow-on improvements for scalability, observability, and integrations.

---

## Status Summary

### ✅ Completed in Current Sprint (2026-02-20)

**Infrastructure & Deployment:**
- ✅ Docker stack deployment (deepresearch) - 11 services operational
- ✅ Monitoring stack deployment (monitoring) - 6 services operational
- ✅ Comprehensive Docker documentation (40,000+ words)
- ✅ Infrastructure validation and health checks
- ✅ Phase 3 Observability Implementation complete (Prometheus, Grafana, AlertManager, Jaeger)
- ✅ Health check configuration and monitoring
- ✅ OpenTelemetry infrastructure setup (collector, exporters)

**Dependencies Deployed & Verified:**
- ✅ **Ollama runtime** (11434) - LLM execution operational
- ✅ **SearXNG + Crawl4AI** (8080, 11235) - Web search and scraping ready
- ✅ **Qdrant** (6333) - Vector storage deployed
- ✅ **Lightning services** (8090) - APO/VERL orchestration ready
- ✅ **Redis** (6379) - Distributed cache operational
- ✅ **InfluxDB** (8086) - Metrics storage operational

---

## Near Term (0-3 Months)

### 🔄 In Progress / Ready for Implementation

- **API and UI surfaces**: ✅ API running on :5000 with health endpoints; 📊 Grafana UI operational on :3001
  - Status: Core API operational, ready for additional endpoints
  - Next: Document API endpoints, add workflow management endpoints

- **Advanced observability**: ✅ Phase 3 implemented - Prometheus (9090), Grafana (3001), AlertManager (9093), Jaeger (16686)
  - Status: Full monitoring stack deployed
  - Next: Implement instrumentation in business logic (agents, workflows)

- **Web search providers**: ✅ Crawl4AI deployed and operational; SearXNG search engine running
  - Status: Infrastructure ready (Crawl4AI :11235, SearXNG :8080)
  - Next: Document `IWebSearchProvider` and resolver wiring, validate SearCrawl4AIAdapter

- **Lightning state services**: ✅ Lightning server deployed with APO/VERL support (:8090)
  - Status: Infrastructure ready, state persistence mechanisms in place
  - Next: Confirm `LightningStateService` + `LightningStore` persistence behaviors

- **Long-running process support**: ✅ Checkpoint infrastructure in place (data/checkpoints/ directory, Redis caching)
  - Status: Pause/resume mechanism exists, needs validation
  - Next: Validate extended execution support with checkpoint recovery

### 📋 Not Yet Started

- **Agent pipeline and middleware**: document and harden `AgentPipelineService` and `AgentMiddleware` behaviors.
  
- **Clarification loop**: productionize `ClarifyAgent`, `ClarifyIterativeAgent`, and `ClarifyAgentAdapter` flows.
  
- **Research briefs and drafting**: validate `ResearchBriefAgent`, `DraftReportAgent`, and `ReportAgent` outputs.
  
- **Workflow adapters**: finalize adapter registration in `AdapterExtensions` and `AdapterRegistrationExtensions`.
  
- **Agent error recovery**: test and document `AgentErrorRecovery` policies.
  
- **Streaming workflows**: verify stream methods across `MasterWorkflow` and `ResearcherWorkflow`.
  
- **Human-in-the-loop support**: provide review, approval, and intervention hooks for research outputs.

---

## Mid Term (3-6 Months)

### 🔄 In Progress / Infrastructure Ready

- **Vector database integration**: ✅ Qdrant deployed (:6333)
  - Status: Vector DB operational, infrastructure ready
  - Next: Validate `QdrantVectorDatabaseService` and `IEmbeddingService` usage paths

- **Agent Lightning orchestration**: ✅ Lightning services deployed with APO/VERL
  - Status: Orchestration services running, ready for expansion
  - Next: Expand `AgentLightningService` and extension hooks for task routing

- **Lightning APO**: ✅ Infrastructure running with APO support
  - Status: LightningAPOConfig in place (:8090)
  - Next: Operationalize `LightningAPOConfig` and `LightningApoScaler` for strategy-based scaling

- **VERL validation**: ✅ Lightning server built with VERL support (cuda variant available)
  - Status: VERL dependencies in Docker image
  - Next: Incorporate `LightningVERLService` into quality checks and reporting

### 📋 Not Yet Started

- **Configuration examples**: refresh `appsettings.websearch.json` and `appsettings.vector-db.example.json`.

---

## Long Term (6-12 Months)

- **Adaptive optimization**: integrate circuit breaker, backpressure, and retry strategies into Lightning APO.
  
- **Hybrid retrieval**: combine vector search with reranking and structured knowledge stores.
  
- **Pluggable LLM backends**: extend beyond Ollama to hosted endpoints with unified config.

---

## Stretch Goals: Vector DBs and Search Strategies

- Support for additional vector databases (currently: Qdrant)
- Evaluate search strategies (Vector, PageIndex, KnowledgeGraph, Hybrid, Adaptive)
- Performance, cost, and risk analysis with ROI and cost-per-accuracy-point comparisons across strategies
- Evaluate leveraging Rust components for performance-critical improvements versus .NET baselines

---

## Milestones

### Current Status

| Milestone | Description | Status | Target |
|-----------|-------------|--------|--------|
| **M0** | Docker infrastructure and observability | ✅ COMPLETE | 2026-02-20 |
| **M1** | Clarification, brief, draft, and report agents validated end-to-end | 📋 PENDING | 2026-03-20 |
| **M2** | Web search provider resolver and adapters fully documented | 🔄 IN PROGRESS | 2026-04-20 |
| **M3** | Vector database integration verified with Qdrant | 🔄 IN PROGRESS | 2026-05-20 |
| **M4** | Lightning APO + VERL integration completed | 🔄 IN PROGRESS | 2026-06-20 |
| **M5** | Observability + optimization layer expanded for production | 🔄 IN PROGRESS | 2026-07-20 |

### M0: Docker & Observability Infrastructure ✅ COMPLETE

**Completed 2026-02-20:**
- ✅ Docker stack "deepresearch" with 11 services deployed
- ✅ Docker stack "monitoring" with 6 services deployed
- ✅ All services validated and healthy
- ✅ Comprehensive Docker documentation (BuildDocs/Docker/)
- ✅ Phase 3 Observability fully implemented
- ✅ All dependencies operational

### M1: Agent Workflows - PENDING

**Target: 2026-03-20**
- Clarification agent pipeline validation
- Research brief generation testing
- Draft report agent validation
- Final report agent testing
- End-to-end workflow validation

### M2: Web Search Integration - IN PROGRESS

**Target: 2026-04-20**
- Document `IWebSearchProvider` interface
- Validate SearCrawl4AIAdapter implementation
- Document provider resolver wiring
- Create configuration examples
- Performance testing and optimization

### M3: Vector Database Integration - IN PROGRESS

**Target: 2026-05-20**
- Validate `QdrantVectorDatabaseService`
- Test `IEmbeddingService` integration
- Document usage patterns
- Performance benchmarking
- Configuration examples

### M4: Lightning Orchestration - IN PROGRESS

**Target: 2026-06-20**
- Operationalize Lightning APO config
- Test `LightningApoScaler` strategies
- Implement VERL quality checks
- Document orchestration patterns
- Load testing and scaling validation

### M5: Production Observability - IN PROGRESS

**Target: 2026-07-20**
- Complete OpenTelemetry instrumentation
- Deploy Grafana dashboards for workflows
- Configure AlertManager notifications
- Production-ready monitoring setup
- SLO definition and tracking

---

## Dependencies

### ✅ All Core Dependencies Deployed & Operational

| Dependency | Port | Status | Notes |
|-----------|------|--------|-------|
| **Ollama** | 11434 | ✅ Running | LLM inference, models available |
| **SearXNG** | 8080 | ✅ Running | Meta search engine operational |
| **Crawl4AI** | 11235 | ✅ Running | Web scraping service operational |
| **Qdrant** | 6333 | ✅ Running | Vector database operational |
| **Lightning Services** | 8090 | ✅ Running | APO/VERL orchestration ready |
| **Redis** | 6379 | ✅ Running | Distributed cache operational |
| **InfluxDB** | 8086 | ✅ Running | Time-series metrics storage |
| **Prometheus** | 9090 | ✅ Running | Metrics collection |
| **Grafana** | 3001 | ✅ Running | Dashboards and visualization |
| **AlertManager** | 9093 | ✅ Running | Alert routing and management |
| **Jaeger** | 16686 | ✅ Running | Distributed tracing |

---

## Documentation Status

### ✅ Completed

- ✅ Docker infrastructure documentation (15,000+ words)
- ✅ Monitoring stack deployment guide
- ✅ Docker validation report
- ✅ Infrastructure index and navigation
- ✅ All documentation organized in BuildDocs/Docker/

### 📋 In Progress

- 📋 API endpoint documentation
- 📋 Web search provider integration guide
- 📋 Vector database integration guide
- 📋 Workflow and agent documentation
- 📋 Configuration examples

### 📋 Pending

- 📋 Runbooks and operational procedures
- 📋 Troubleshooting guides for production
- 📋 Performance tuning documentation
- 📋 Scaling guidelines

---

## Next Steps (Immediate Actions)

1. **Validate Agent Pipelines** (Week 1-2)
   - Test `ClarifyAgent` → `ResearchBriefAgent` → `DraftReportAgent` → `ReportAgent` flow
   - Document any issues and required hardening

2. **Document Web Search Integration** (Week 2-3)
   - Write `IWebSearchProvider` interface documentation
   - Document SearCrawl4AIAdapter implementation
   - Create usage examples and configuration guide

3. **Validate Vector Database** (Week 3-4)
   - Test Qdrant integration with embedding service
   - Document usage patterns and configuration
   - Performance test with sample data

4. **Implement Workflow Instrumentation** (Week 4)
   - Add OpenTelemetry instrumentation to agent services
   - Create Grafana dashboards for workflow metrics
   - Configure AlertManager for critical alerts

5. **Update Configuration Examples** (Ongoing)
   - Create appsettings.websearch.json with commented examples
   - Create appsettings.vector-db.json with commented examples
   - Document all configuration options

---

**Status**: ✅ Infrastructure Ready | 📋 Near-Term Development  
**Last Updated**: 2026-02-20  
**Next Review**: 2026-03-06
