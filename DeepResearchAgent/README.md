# Deep Research Agent - C# Implementation

## Project Overview

A sophisticated multi-agent research system converted from Python (LangGraph) to C# using Microsoft Agent Framework. Features a supervisor-coordinated diffusion process with parallel researchers, adversarial red team feedback, and self-evolution quality scoring.

## Architecture

### Core Components

- **Master Workflow**: User interaction → Research brief generation → Draft creation → Supervisor loop → Final report
- **Supervisor Agent**: Coordinates research, manages state, handles diffusion iterations
- **Researcher Sub-Agents**: Parallel workers conducting focused research tasks
- **Red Team**: Adversarial critique for self-correction
- **Context Pruner**: Knowledge base management and fact extraction

### State Management

- `FactState`: Atomic knowledge units with provenance tracking
- `CritiqueState`: Adversarial feedback structure
- `SupervisorState`: Main coordination state
- `ResearcherState`: Worker agent state
- `AgentState`: Top-level workflow state

## Technology Stack

- **.NET 8.0**: Modern C# runtime
- **Microsoft.Extensions.AI** (v10.1.1): AI abstraction layer
- **Microsoft.Agents.AI / Workflows** (v1.0.0-preview.260108.1): Agent + workflow orchestration
- **OllamaSharp** (v5.4.12): Local LLM integration
- **Microsoft.Extensions.DependencyInjection** (v10.0.2): DI container
- **SearXNG + Crawl4AI**: Dockerized web search and scraping backend for research
- **LightningStore**: File-backed knowledge persistence for facts
- **Test Project**: xUnit + Moq for service/unit tests

## Prerequisites

1. **.NET 8 SDK**: [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
2. **Ollama**: Local LLM runtime
   ```bash
   # Install Ollama (Linux/macOS)
   curl -fsSL https://ollama.com/install.sh | sh
   
   # Pull model
   ollama pull mistral  # or your preferred model
   ```
3. **Docker** (optional): For containerized deployment (includes SearXNG and Crawl4AI)

## Quick Start

### Local Development

```bash
# Navigate to project directory
cd DeepResearchAgent

# Restore dependencies
dotnet restore

# Build project
dotnet build

# Run application
dotnet run
```

### Docker Deployment

```bash
# Build and run with docker-compose (agent + Ollama + Crawl4AI + SearXNG)
docker-compose up --build

# Run in detached mode
docker-compose up -d

# View logs
docker-compose logs -f deep-research-agent

# Stop services
docker-compose down
```

### Web Search Services (Docker)

```bash
# Start only search-related services
docker-compose up -d searxng crawl4ai

# Verify endpoints
curl "http://localhost:8080/search?q=test&format=json"   # SearXNG
curl -X POST "http://localhost:8000/crawl" -H "Content-Type: application/json" -d '{"urls":["https://example.com"]}'  # Crawl4AI
```

### Running Tests

```bash
# Run unit tests
dotnet test DeepResearchAgent.Tests/DeepResearchAgent.Tests.csproj
```

## Configuration

### Ollama Connection

Edit `appsettings.json` to configure Ollama endpoint:

```json
{
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "DefaultModel": "mistral"
  }
}
```

### Web Search & Scraping

Environment variables (docker-compose already sets sensible defaults):

- `SEARXNG_BASE_URL` (default `http://localhost:8080`)
- `CRAWL4AI_BASE_URL` (default `http://localhost:8000`)

### Knowledge Persistence (LightningStore)

- Default path: `data/lightningstore.json`
- Configure via `LightningStoreOptions` (data directory and file name)
- Stores facts as JSON for easy inspection and portability

### Research Parameters

Adjust in `Program.cs`:

```csharp
var config = new ResearchConfig
{
    MaxConcurrentResearchers = 3,
    MaxResearchIterations = 10,
    QualityThreshold = 7.0f
};
```

## Project Structure

```
DeepResearchAgent/
├── Models/              # State classes & DTOs
│   ├── AgentState.cs
│   ├── FactState.cs
│   ├── CritiqueState.cs
│   ├── SearchResult.cs
│   ├── ScrapedContent.cs
│   └── ...
├── Workflows/           # Workflow executors (planned)
│   ├── MasterWorkflow.cs
│   ├── SupervisorWorkflow.cs
│   └── ResearcherWorkflow.cs
├── Tools/               # Agent tools
│   └── ResearchTools.cs
├── Services/            # External integrations
│   ├── OllamaService.cs
│   ├── SearCrawl4AIService.cs
│   ├── SearCrawl4AIConfig.cs
│   └── LightningStore.cs
├── Prompts/             # LLM prompts
│   └── PromptTemplates.cs
├── Config/              # DI setup
│   └── ServiceConfiguration.cs
└── Program.cs           # Entry point
```

## Features

### 1. Multi-Agent Coordination
- Supervisor delegates tasks to parallel researchers
- Each researcher operates independently with its own state
- Results aggregated and synthesized

### 2. Diffusion-Based Refinement
- Initial "noisy" draft progressively refined
- Iterative improvement through research and synthesis
- Quality-driven termination

### 3. Self-Correction
- **Red Team**: Adversarial critique of drafts
- **Quality Scoring**: LLM-as-judge evaluation
- **Context Pruning**: Structured fact extraction from raw notes

### 4. Knowledge Management
- Temporary raw notes buffer (high-volume, unprocessed)
- Permanent knowledge base (structured, curated facts)
- Provenance tracking for all information
- LightningStore persists facts to disk as JSON

### 5. Web Search & Scraping (SearXNG + Crawl4AI)
- `SearCrawl4AIService` combines metasearch with robust scraping
- Tools available via `ResearchTools.WebSearch` and `ResearchTools.DeepWebSearch`
- Dockerized services for reproducible, privacy-respecting research pipelines
- Built-in in-memory caching for repeated queries (configurable duration)

### 6. Streaming Output
- Master/Supervisor/Researcher workflows expose streaming methods for real-time progress updates
- `Program.cs` demo streams live updates from the master workflow

## Usage Example

```csharp
var agent = new DeepResearchAgent(ollamaService, config);

var query = @"Analyze the impact of quantum computing on 
cryptography by 2030. Focus on RSA and elliptic curve 
vulnerabilities.";

var result = await agent.ResearchAsync(query);

Console.WriteLine(result.FinalReport);
```

### Web Search Tooling

```csharp
var httpClient = new HttpClient();
var search = new SearCrawl4AIService(httpClient);
var store = new LightningStore();

// Quick fact-finding
var summary = await ResearchTools.WebSearch("AI agents", 5, search);

// Deep research with full-page scraping
var deep = await ResearchTools.DeepWebSearch("transformer architecture", 3, search);

// Persist a fact
await store.SaveFactAsync(new FactState
{
    Content = "Transformers rely on self-attention mechanisms",
    SourceUrl = "https://example.com/transformers",
    ConfidenceScore = 90
});
```

## Development Notes

### Python → C# Conversion

| Python (LangGraph) | C# (Microsoft AF) |
|-------------------|-------------------|
| `TypedDict` with `Annotated` | C# classes with properties |
| `operator.add` | Manual list merge logic |
| `@tool` decorator | `[Description]` attributes + AIFunctionFactory |
| `asyncio.gather()` | `Task.WhenAll()` |
| `Command[Literal[...]]` | Executor routing logic |

### Async Patterns

- All workflow executors use `async/await`
- True parallelism via `Task.WhenAll()` (not Python's pseudo-concurrency)
- `ConfigureAwait(false)` for library code

### Microsoft.Agents.AI Packages

The following Microsoft.Agents.AI packages are used for agent and workflow orchestration:

```bash
dotnet add package Microsoft.Agents.AI --version 1.0.0-preview.260108.1
dotnet add package Microsoft.Agents.AI.Workflows --version 1.0.0-preview.260108.1
```

#### Workflow Orchestration (Microsoft.Agents.AI.Workflows)

Installed packages:

```bash
dotnet add package Microsoft.Agents.AI --version 1.0.0-preview.260108.1
dotnet add package Microsoft.Agents.AI.Workflows --version 1.0.0-preview.260108.1
```

Current wiring:
- `Program.cs`: Registers `MasterWorkflow`, `SupervisorWorkflow`, `ResearcherWorkflow` plus Ollama, SearCrawl4AI, LightningStore, HttpClient.
- `Workflows/`: executors with domain-specific logic, prompts, and state transitions that coordinate search/scrape and persist extracted facts.


## Roadmap

- [x] Implement web search & scraping (SearXNG + Crawl4AI)
- [x] Add LightningStore for knowledge persistence
- [x] Complete workflow orchestration with Microsoft.Agents.AI.Workflows
- [x] Add streaming support for real-time output
- [x] Implement caching for repeated queries
- [x] Integrate optional Tavily API search
- [ ] Add unit tests
- [ ] Performance benchmarking

## Contributing

This is a research prototype. Contributions welcome for:
- Tool integrations (web search, crawling)
- Workflow optimization
- Prompt engineering improvements
- Testing and validation

## License

[MIT License]

## Acknowledgments

- Original Python implementation: LangGraph-based deep research agent


## State Management Architecture

The state management layer is a critical component that bridges Python's LangGraph hierarchical state patterns to C#. It provides:

### Core Components

#### 1. **State Models** (in `/Models/`)
- `AgentState`: Top-level workflow state with message history and research artifacts
- `SupervisorState`: Hierarchical state for the diffusion loop with knowledge base and quality tracking
- `ResearcherState`: Worker agent state with research topic and raw notes
- `FactState`: Atomic knowledge units with provenance and confidence scoring
- `CritiqueState`: Adversarial feedback structure with severity tracking
- `QualityMetric`: Quality evaluation snapshots for convergence analysis

#### 2. **StateAccumulator<T>**
Implements Python's `add_messages` and `operator.add` patterns, enabling:
- Thread-safe list accumulation for messages, notes, and facts
- Merge semantics for multi-agent state updates
- Read-only views of accumulated items

```csharp
// Example: Accumulating research notes
var notes = new StateAccumulator<string>();
notes.Add("Finding 1");
notes.Add("Finding 2");
var merged = notes + otherNotesAccumulator;
```

#### 3. **StateFactory**
Provides consistent state initialization:

```csharp
// Create and initialize states
var agentState = StateFactory.CreateAgentState(initialMessages);
var supervisorState = StateFactory.CreateSupervisorState(brief, draft, messages);
var researcherState = StateFactory.CreateResearcherState("research topic");

// Factory methods for domain objects
var fact = StateFactory.CreateFact(content, url, confidence);
var critique = StateFactory.CreateCritique(author, concern, severity);
var metric = StateFactory.CreateQualityMetric(score, feedback, iteration);
```

#### 4. **StateValidator**
Validates state consistency throughout workflows:

```csharp
// Validate before state transitions
var validation = StateValidator.ValidateAgentState(state);
if (!validation.IsValid)
{
    foreach (var error in validation.Errors)
        Console.WriteLine(error);
}

// Check supervisor health
var health = StateValidator.GetHealthReport(supervisorState);
bool shouldContinue = StateValidator.ShouldContinueDiffusion(state, maxIterations: 10);
```

#### 5. **StateManager**
Tracks state progression and enables transactional updates:

```csharp
var manager = new StateManager();

// Capture snapshots at each iteration
var snapshot1 = manager.CaptureSnapshot(state, "phase1");
var snapshot2 = manager.CaptureSnapshot(state, "phase2");

// Merge states from multiple agents
manager.MergeSupervisorState(targetState, sourceState);

// Retrieve history for debugging
var history = manager.GetHistory();
var supervisorHistory = manager.GetSupervisorHistory();
```

#### 6. **StateTransition & Routing**
Implements LangGraph's `Command[Literal[...]]` pattern for workflow orchestration:

```csharp
// Create routing logic similar to Python's conditional edges
var router = new StateTransitionRouter();

// Deterministic edges
router.RegisterEdge("node1", "node2");

// Conditional routing
router.RegisterConditionalEdge("decision",
    state => string.IsNullOrEmpty(state.ResearchBrief) ? "clarify" : "research");

// Parallel nodes (Red Team + Context Pruner)
router.RegisterParallelEdge("supervisor_tools", "red_team", "context_pruner");

// Get next transition
var transition = router.GetNextTransition("current_node", state);
if (transition is ParallelTransition parallel)
{
    var nodes = parallel.GetParallelNodes(); // Execute in parallel
}
```

### State Flow Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│ AgentState (User interaction)                                   │
│ ├─ messages: List<ChatMessage>      (conversation history)      │
│ ├─ research_brief: string           (user's research question)  │
│ ├─ supervisor_messages: List        (diffusion loop history)    │
│ ├─ raw_notes: List<string>          (temp buffer)               │
│ ├─ notes: List<string>              (curated findings)          │
│ ├─ draft_report: string             (initial + refined versions)│
│ └─ final_report: string             (output)                    │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│ SupervisorState (Diffusion process)                             │
│ ├─ research_brief: string           (from AgentState)           │
│ ├─ draft_report: string             (iteratively refined)       │
│ ├─ knowledge_base: List<FactState>  (permanent, structured)     │
│ ├─ raw_notes: List<string>          (temporary buffer)          │
│ ├─ active_critiques: List<Critique> (Red Team feedback)         │
│ ├─ quality_history: List<Metric>    (convergence tracking)      │
│ └─ research_iterations: int         (loop counter)              │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│ ResearcherState (Parallel worker agents)                        │
│ ├─ research_topic: string           (delegated task)            │
│ ├─ researcher_messages: List        (ReAct loop history)        │
│ ├─ raw_notes: List<string>          (unprocessed findings)      │
│ ├─ compressed_research: string      (final summary)             │
│ └─ tool_call_iterations: int        (prevent infinite loops)    │
└─────────────────────────────────────────────────────────────────┘
```

### Key Design Patterns

1. **Accumulator Pattern** (Python's `operator.add`)
   - Lists are merged, not replaced
   - Enables multi-agent contributions to shared state
   - Thread-safe for concurrent agent execution

2. **Factory Pattern** (StateFactory)
   - Consistent initialization across workflows
   - Validation built into creation
   - Cloning for state snapshots

3. **Validator Pattern** (StateValidator)
   - Prevents invalid state transitions
   - Reports all errors together
   - Checks specific invariants (e.g., confidence 1-100)

4. **Snapshot Pattern** (StateManager)
   - Immutable point-in-time views
   - Enables rollback and debugging
   - Tracks convergence via quality history

5. **Router Pattern** (StateTransition* classes)
   - Declarative workflow definition
   - Supports deterministic, conditional, and parallel routing
   - Maps to LangGraph's node connectivity
