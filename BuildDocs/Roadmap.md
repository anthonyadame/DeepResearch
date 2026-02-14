# Roadmap

## Objectives

- Capture current capabilities implemented in the codebase.
- Prioritize stabilization, documentation, and test coverage for existing features.
- Plan follow-on improvements for scalability, observability, and integrations.

## Near Term (0-3 Months)

- **Agent pipeline and middleware**: document and harden `AgentPipelineService` and `AgentMiddleware` behaviors.
- **Clarification loop**: productionize `ClarifyAgent`, `ClarifyIterativeAgent`, and `ClarifyAgentAdapter` flows.
- **Research briefs and drafting**: validate `ResearchBriefAgent`, `DraftReportAgent`, and `ReportAgent` outputs.
- **Workflow adapters**: finalize adapter registration in `AdapterExtensions` and `AdapterRegistrationExtensions`.
- **Lightning state services**: confirm `LightningStateService` + `LightningStore` persistence behaviors.
- **Agent error recovery**: test and document `AgentErrorRecovery` policies.
- **Streaming workflows**: verify stream methods across `MasterWorkflow` and `ResearcherWorkflow`.
- **Web search providers**: document `IWebSearchProvider` and resolver wiring, including `SearCrawl4AIAdapter`.
- **Long-running process support**: add resiliency for extended executions with pause/resume checkpoints.
- **API and UI surfaces**: add endpoints and a lightweight UI to support testing and monitoring.
- **Human-in-the-loop support**: provide review, approval, and intervention hooks for research outputs.

## Mid Term (3-6 Months)

- **Vector database integration**: validate `QdrantVectorDatabaseService` and `IEmbeddingService` usage paths.
- **Agent Lightning orchestration**: expand `AgentLightningService` and extension hooks for task routing.
- **Lightning APO**: operationalize `LightningAPOConfig` and `LightningApoScaler` for strategy-based scaling.
- **VERL validation**: incorporate `LightningVERLService` into quality checks and reporting.
- **Configuration examples**: refresh `appsettings.websearch.json` and `appsettings.vector-db.example.json`.

## Long Term (6-12 Months)

- **Adaptive optimization**: integrate circuit breaker, backpressure, and retry strategies into Lightning APO.
- **Advanced observability**: production OpenTelemetry wiring and dashboards for workflows.
- **Hybrid retrieval**: combine vector search with reranking and structured knowledge stores.
- **Pluggable LLM backends**: extend beyond Ollama to hosted endpoints with unified config.

## Stretch goals Vector DBs and Search Strategies
- Support for additional vector databases
- Evaluate search strategies (Vector, PageIndex, KnowledgeGraph, Hybrid, Adaptive).
- Performance, cost, and risk analysis with ROI and cost-per-accuracy-point comparisons across strategies.
- Evaluate leveraging Rust components for performance-critical improvements versus .NET baselines.

## Milestones

- **M1**: Clarification, brief, draft, and report agents validated end-to-end.
- **M2**: Web search provider resolver and adapters fully documented.
- **M3**: Vector database integration verified with Qdrant.
- **M4**: Lightning APO + VERL integration completed.
- **M5**: Observability + optimization layer expanded for production.

## Dependencies

- **Ollama runtime** for LLM execution.
- **SearXNG + Crawl4AI** for web search and scraping.
- **Qdrant** (optional) for vector storage.
- **Lightning services** for state persistence and APO/VERL support.
