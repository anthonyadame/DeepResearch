# Deep Research Agent 🔬

A sophisticated multi-agent research system that conducts in-depth investigations on topics, refines findings through iterative feedback, and synthesizes comprehensive reports.

## 🎯 Tagline
### Better answers, through a better ask.

## 📚 Documentation


**Quick Links:**
- 📐 [**Architecture.md**](BuildDocs/Architecture.md) - System architecture overview
- 👔 [**Executive_Summary.md**](BuildDocs/Executive_Summary.md) - Executive-level summary
- 🧭 [**Roadmap.md**](BuildDocs/Roadmap.md) - Planned milestones and timelines


See [BuildDocs/](BuildDocs/) for complete planning documentation.

---

## 🎯 Overview

The Deep Research Agent implements a **diffusion-based research refinement loop** that:

1. **Clarifies** user intent and research goals
2. **Researches** topics comprehensively using web search and scraping
3. **Evaluates** findings for quality and accuracy
4. **Refines** through adversarial critique (red team)
5. **Synthesizes** into professional research reports

## 🏗️ Architecture

### Core Workflows

- **MasterWorkflow** - Orchestrates the entire 5-step research pipeline
- **SupervisorWorkflow** - Manages iterative refinement through diffusion loop
- **ResearcherWorkflow** - Conducts focused research on specific topics
- **SearCrawl4AIService** - Handles web search and content scraping

### Workflow Services (NEW)

New service layer for enhanced workflow capabilities:

- **MasterWorkflowService** - Advanced orchestration of multi-agent workflows with checkpoint support
- **ResearcherWorkflowService** - Intelligent research loop management with VERL integration
- **PerformanceAnalyticsService** - Tracks metrics and optimization recommendations
- **SemanticSearchService** - Vector-based semantic search across research findings
- **StateAccumulatorService** - Manages workflow state accumulation and diffusion

### Agent Adapters (NEW)

Framework-compatible adapters for agent integration:

- **AnalystAgentAdapter** - Wraps AnalystAgent for AIAgentBuilder integration
- **DraftReportAgentAdapter** - Wraps DraftReportAgent for report generation
- **ReportAgentAdapter** - Wraps ReportAgent for final report finalization

### State Management

- **LightningStateService** - High-performance state caching and persistence
- **StateFactory** - Creates properly initialized state objects
- **SupervisorState** - Tracks research progress and findings

### LLM Integration

- **OllamaService** - Unified LLM interface with model support
- **WorkflowModelConfiguration** - Model selection for each workflow function

## 🚀 Quick Start

### Installation

1. **Clone the repository:**
```bash
git clone https://github.com/anthonyadame/DeepResearch.git
cd DeepResearchAgent
```

2. **Build the solution:**
```bash
dotnet build
```

3. **Install Ollama** (for local LLM):
   - Download from https://ollama.ai
   - Pull a model: `ollama pull gpt-oss:20b`

4. **Setup SearXNG** Web Search:
   - Edit the .env.example file to set the hostname and an email
   - Rename it to .env
   - Generate the secret key (See Below)
  
5. **Setup SearXNG** Web Search (Alternative setup):
   - [Install docker](https://docs.docker.com/install/)
   - Get searxng-docker

   ```shell
   git clone https://github.com/searxng/searxng-docker.git
   cd searxng-docker
   ```
   - Edit the .env file to set the hostname and an email
   - Generate the secret key
   - Linux/MacOS:
   
   ```shell
   # Generate a random 32-byte hex string and replace 'ultrasecretkey' in settings.yml 
   
   # Linux
   sed -i "s|ultrasecretkey|$(openssl rand -hex 32)|g" searxng/settings.yml
   
   # MacOS
   sed -i '' "s|ultrasecretkey|$(openssl rand -hex 32)|g" searxng/settings.yml
   ```
   
   > [!NOTE]
   > Windows users can use the following powershell script to generate the secret key:
   > ```powershell
   > $randomBytes = New-Object byte[] 32
   > (New-Object Security.Cryptography.RNGCryptoServiceProvider).GetBytes($randomBytes)
   > $secretKey = -join ($randomBytes | ForEach-Object { "{0:x2}" -f $_ })
   > (Get-Content searxng/settings.yml) -replace 'ultrasecretkey', $secretKey | Set-Content searxng/settings.yml
   > ```



### Basic Usage

```csharp
// Setup services
var services = new ServiceCollection();
services.AddScoped<ILightningStateService, LightningStateService>();
services.AddScoped<OllamaService>();
services.AddScoped<ResearcherWorkflow>();
services.AddScoped<SupervisorWorkflow>();
services.AddScoped<MasterWorkflow>();

var provider = services.BuildServiceProvider();
var master = provider.GetRequiredService<MasterWorkflow>();

// Run research
string query = "Latest developments in quantum computing";
string result = await master.RunAsync(query);
Console.WriteLine(result);
```

### Using Workflow Services & Adapters (NEW)

The new workflow services provide enhanced capabilities for performance monitoring and agent adaptation:

#### Performance Analytics

```csharp
var analytics = provider.GetRequiredService<PerformanceAnalyticsService>();

// Record workflow metrics
var metrics = new WorkflowMetrics
{
    WorkflowId = "research-001",
    TotalExecutionTime = TimeSpan.FromMinutes(5),
    QualityScore = 0.95,
    IterationCount = 3,
    ApiCallCount = 12
};

await analytics.RecordWorkflowMetricsAsync("research-001", metrics);
```

#### Semantic Search

```csharp
var semanticSearch = provider.GetRequiredService<SemanticSearchService>();

// Search research findings semantically
var results = await semanticSearch.SearchAsync(
    query: "quantum computing applications",
    topK: 10);
```

#### Agent Adapters

```csharp
// Access agent adapters for framework integration
var analystAdapter = provider.GetRequiredService<AnalystAgentAdapter>();
var draftAdapter = provider.GetRequiredService<DraftReportAgentAdapter>();
var reportAdapter = provider.GetRequiredService<ReportAgentAdapter>();
```

## 📊 Key Features

### 1. Intelligent Research
- **Web Search & Scraping** - Real-time information gathering
- **ReAct Loop** - Reasoning and acting based on findings
- **Fact Extraction** - Automated knowledge base population

### 2. Quality Assurance
- **Red Team Critique** - Adversarial evaluation for weaknesses
- **Quality Scoring** - Multi-dimensional quality assessment
- **Convergence Detection** - Stops when quality threshold reached

### 3. Model Flexibility
- **Multi-Model Support** - Use different models for different tasks
- **Cost Optimization** - Fast/cheap models for coordination
- **Quality Optimization** - Powerful models for reasoning
- **Custom Profiles** - Create your own model configurations

### 4. Performance
- **LightningStore** - Fast in-memory caching
- **Parallel Execution** - Concurrent research tasks
- **State Persistence** - Resume from interruptions

## 🎨 Configuration


### Model Configuration

```csharp
// Default (optimized for balance)
var config = new WorkflowModelConfiguration();

// Cost-optimized (fast/cheap)
var config = new WorkflowModelConfiguration
{
    SupervisorBrainModel = "mistral:7b",
    SupervisorToolsModel = "mistral:latest",
    QualityEvaluatorModel = "mistral:7b",
    RedTeamModel = "mistral:7b",
    ContextPrunerModel = "orca-mini:latest"
};

// Quality-optimized (best results)
var config = new WorkflowModelConfiguration
{
    SupervisorBrainModel = "neural-chat:13b",
    SupervisorToolsModel = "neural-chat:7b",
    QualityEvaluatorModel = "neural-chat:13b",
    RedTeamModel = "neural-chat:13b",
    ContextPrunerModel = "neural-chat:7b"
};
```

### Available Models

| Function | Model | Purpose |
|----------|-------|---------|
| Supervisor Brain | `gpt-oss:20b` | Complex reasoning |
| Supervisor Tools | `mistral:latest` | Fast coordination |
| Quality Evaluator | `gpt-oss:20b` | Detailed analysis |
| Red Team | `gpt-oss:20b` | Critical thinking |
| Context Pruner | `mistral:latest` | Quick extraction |


### Building

```bash
# Debug build
dotnet build

# Release build
dotnet build --configuration Release

# Run specific project
dotnet run --project DeepResearchAgent
```

## 📋 Requirements

- **.NET 8** or higher
- **Ollama** for local LLM (or API endpoint)
- **Internet connection** for web search functionality
- **Docker Desktop** (optional)


## 🔗 External Services

### Ollama
- **Purpose:** Local LLM inference
- **Models:** gpt-oss:20b, mistral:latest, neural-chat:13b, etc.
- **Setup:** Download from https://ollama.ai

### Crawl4AI
- **Purpose:** Web scraping and content extraction
- **Features:** JavaScript rendering, markdown conversion

## 📈 Performance

### Optimization Tips

1. **Use Cost-Optimized Profile** for quick prototyping
2. **Use Quality-Optimized Profile** for critical research
3. **Adjust max iterations** based on time constraints
4. **Cache results** to avoid redundant searches


## 🐛 Troubleshooting

### Common Issues

**"Cannot connect to Ollama"**
- Ensure Ollama is running: `ollama serve`
- Check endpoint: Default is `http://localhost:11434`

**"Model not found"**
- Pull the model: `ollama pull gpt-oss:20b`
- Verify model name in configuration

**"Tests failing"**
- Clean build: `dotent clean && dotnet build`
- Run single test: `dotnet test --filter "TestName"`

## 📝 License

See `LICENSE.txt` for license information.

## 🤝 Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Run tests: `dotnet test`
5. Submit a pull request

## 📞 Support

For issues, questions, or suggestions:
- Open an issue on GitHub


## 📖 Citation

This project is inspired by and based on the research and concepts presented in:

**Khan, Fareed.** "Building a Human-Level Deep Research Agentic System using the Time Test Diffusion Algorithm: ReAct Loops, Denoising, Parallel Research and more." *Level Up Coding* (Medium), 2024.  
🔗 [Read the article](https://levelup.gitconnected.com/building-a-human-level-deep-research-agentic-system-using-the-time-test-diffusion-algorithm-587ed3c225a0)  
👤 [Author Profile](https://medium.com/@fareedkhandev)

## 📚 References

The implementation incorporates techniques and insights from the following publications:

### LLM Optimization & Reasoning


**Alexia Jolicoeur-Martineau.** "Less is More: Recursive Reasoning with Tiny Networks."  
🔗 [Read the paper](https://arxiv.org/pdf/2510.04871)

**LinkedIn AI & HuggingFace.** "GPT-OSS: Open-Source Models for Agentic Reinforcement Learning."  
🔗 [Read the article](https://huggingface.co/blog/LinkedIn/gpt-oss-agentic-rl)  
*Presents GPT-OSS models optimized for agentic workflows and reinforcement learning applications*

---


Copyright (c) 2026 Anthony Adame

MIT License.
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)


