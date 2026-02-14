# Architecture

## Overview

The Deep Research Agent is a multi-workflow system that runs a diffusion-based research refinement loop. It orchestrates intent clarification, research, evaluation, critique, and synthesis while persisting state for recovery and iterative improvement.

## Core Workflows

- `MasterWorkflow`: Entry point that coordinates the full research pipeline.
- `SupervisorWorkflow`: Manages iterative refinement and quality convergence.
- `ResearcherWorkflow`: Performs focused research tasks and evidence gathering.
- `SearCrawl4AIService`: Executes search and content extraction.

### Workflow Flow

```mermaid
flowchart TD
    A[User Query] --> B[MasterWorkflow]
    B --> C[SupervisorWorkflow]
    C --> D[ResearcherWorkflow]
    D --> E[SearCrawl4AIService]
    E --> D
    D --> C
    C --> F[Quality Evaluator]
    F -->|Meets Threshold| G[Synthesis]
    F -->|Needs Refinement| C
    G --> H[Final Report]
```

## State Management

State is stored and retrieved via `LightningStateService` and initialized through `StateFactory`. The `SupervisorState` tracks iteration progress, findings, and quality metrics.

```mermaid
sequenceDiagram
    participant MW as MasterWorkflow
    participant SF as StateFactory
    participant LS as LightningStateService
    participant SW as SupervisorWorkflow

    MW->>SF: Create initial state
    SF-->>MW: SupervisorState
    MW->>LS: Persist state
    MW->>SW: Run with state
    SW->>LS: Update state snapshots
    LS-->>SW: Latest state
```

## LLM Integration

LLM tasks are routed through `OllamaService` with `WorkflowModelConfiguration` defining which models handle reasoning, tools, evaluation, and pruning.

```mermaid
flowchart LR
    Q[Prompt] --> O[OllamaService]
    O --> B[Supervisor Brain Model]
    O --> T[Supervisor Tools Model]
    O --> E[Quality Evaluator Model]
    O --> R[Red Team Model]
    O --> P[Context Pruner Model]
```

## Data Flow

```mermaid
flowchart TB
    subgraph User
        U[User]
    end

    subgraph Orchestration
        MW[MasterWorkflow]
        SW[SupervisorWorkflow]
        RW[ResearcherWorkflow]
    end

    subgraph Services
        SC[SearCrawl4AIService]
        OS[OllamaService]
        LS[LightningStateService]
    end

    U --> MW
    MW --> SW
    SW --> RW
    RW --> SC
    RW --> OS
    SW --> OS
    MW --> LS
    SW --> LS
    RW --> LS
    SW --> MW
    MW --> U
```

## Deployment Considerations

- **Runtime**: .NET 8 or later.
- **LLM Endpoint**: Local `OllamaService` or remote compatible API.
- **Persistence**: Ensure `LightningStateService` backing store is durable for long runs.
- **Monitoring**: Optional Grafana/Prometheus stack for APO metrics.
- **Scaling**: Adjust concurrency limits through APO configuration for workload size.
