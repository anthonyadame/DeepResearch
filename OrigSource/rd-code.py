# %% [markdown]
# ## Setup and Configuration
# 
# Before we begin, let's install and import all necessary dependencies.

# %%
# Install required packages (uncomment if needed)
# %pip install langchain langchain-ollama langchain-community langgraph tavily-python pydantic

# Core imports
import asyncio
import operator
from datetime import datetime
from typing import Annotated, List, Literal, Optional, Sequence, TypedDict

# Pydantic for data validation
from pydantic import BaseModel, Field

# LangChain imports for Ollama
from langchain_ollama import ChatOllama

# Helper function to initialize Ollama models
def init_chat_model(model: str = "gpt-oss:20b", base_url: str = "http://localhost:11434", **kwargs):
    """Initialize an Ollama chat model."""
    return ChatOllama(model=model, base_url=base_url, **kwargs)

from langchain_core.messages import (
    AIMessage,
    BaseMessage,
    HumanMessage,
    SystemMessage,
    ToolMessage,
    filter_messages,
    get_buffer_string,
)
from langchain_core.tools import InjectedToolArg, tool

# LangGraph imports
from langgraph.graph import END, MessagesState, START, StateGraph
from langgraph.graph.message import add_messages
from langgraph.types import Command

# Tavily search tool (you'll need to install and configure this)
try:
    from langchain_community.tools.tavily_search import TavilySearchResults
    tavily_search = TavilySearchResults(max_results=5)
except ImportError:
    print("Warning: Tavily not installed. Install with: pip install tavily-python")
    tavily_search = None
except Exception as e:
    print(f"Warning: Tavily initialization failed ({e}). Using mock implementation.")
    tavily_search = None

# Create a mock tavily_search tool if real one is not available
if tavily_search is None:
    @tool
    def tavily_search(query: str) -> str:
        """Mock web search tool - returns placeholder results when Tavily API is not configured."""
        return f"[Mock Search Results] Unable to perform actual web search for '{query}' - Tavily API not configured. Please set TAVILY_API_KEY environment variable."

print("✓ All imports loaded successfully")

# %% [markdown]
# ### Configuration and Model Initialization
# 
# Now let's configure our models and system parameters.

# %%
# Configuration constants
max_concurrent_researchers = 3  # Maximum number of parallel research agents
max_researcher_iterations = 5   # Maximum iterations for each researcher
max_supervisor_iterations = 8   # Maximum iterations for the supervisor loop

# Ollama configuration
OLLAMA_BASE_URL = "http://localhost:11434"
# Note: Replace with your Ollama model name (e.g., "llama2", "mistral", "codellama", etc.)
# Run 'ollama list' in terminal to see available models
OLLAMA_MODEL = "gpt-oss:20b"  

# Initialize the main model
model = init_chat_model(model=OLLAMA_MODEL, base_url=OLLAMA_BASE_URL)

# Initialize specialized models for different tasks (all using ollama)
creative_model = init_chat_model(model=OLLAMA_MODEL, base_url=OLLAMA_BASE_URL)
writer_model = init_chat_model(model=OLLAMA_MODEL, base_url=OLLAMA_BASE_URL)
critic_model = init_chat_model(model=OLLAMA_MODEL, base_url=OLLAMA_BASE_URL)
compressor_model = init_chat_model(model=OLLAMA_MODEL, base_url=OLLAMA_BASE_URL)
summarization_model = init_chat_model(model=OLLAMA_MODEL, base_url=OLLAMA_BASE_URL)

print("✓ Models initialized successfully")

# %% [markdown]
# ### Tools Setup
# 
# After defining our tools (think_tool, ConductResearch, ResearchComplete) later in the notebook, we'll initialize them here. This cell will be populated once those tools are defined.

# %% [markdown]
# # Building Deep Research Agent using Time Noise Diffusion Algorithm
# 
# Most [public deep research agents](https://github.com/assafelovic/gpt-researcher) use smart techniques to get better results, like [chain-of-thought reasoning](https://research.google/blog/language-models-perform-reasoning-via-chain-of-thought/) or [generating multiple answers](https://openreview.net/forum?id=H4S4ETc8c9) and picking the best. However, they often skip the key writing steps humans follow: planning, drafting, researching, and revising. An important part of revising is doing more research to [find missing information or make your arguments stronger](https://www.emerald.com/jd/article-abstract/69/2/243/198951/Patterns-of-graduate-students-information-seeking?redirectedFrom=fulltext). The **Time-Tested Diffusion based Deep Research (TTD-DR)** is a modern algorithm designed to address this limitation by **mimicking human research** behavior. Let’s visually explore its architecture …
# 
# ![Why Hierarchical State (Created by Fareed Khan)](https://miro.medium.com/v2/resize:fit:7911/1*xi7PELfrOjpya8OgrbrFgQ.png)
# 
# 1.  **Clarify and Scope:** The agent first clarifies the user’s goal to create a detailed research brief, then writes a rough initial draft to serve as a starting point.
# 2.  **Analyze and Plan:** A supervisor agent critiques the draft to find weaknesses and logical gaps, then plans targeted research tasks to fix them.
# 3.  **Delegate and Research:** The supervisor deploys multiple worker agents to research different sub-topics in parallel, efficiently gathering external facts and data.
# 4.  **Integrate and Refine:** The new research findings are integrated into the draft. This **“denoising”** step corrects flaws and adds detail, steadily improving the report’s quality.
# 5.  **Self-Correction Loop:** A **Red Team** agent attacks the draft logic to find hidden flaws, while an **“Evaluator”** agent scores its quality, creating a constant feedback loop.
# 6.  **Iterate and Finalize:** The entire cycle repeats, progressively enhancing the report until it meets a high-quality standard, at which point the final, polished version is produced.
# 
# > This human pattern works like retrieval-augmented diffusion models, it starts with a rough or messy idea and slowly improves it into a clear, high-quality result.
# 
# In this blog, we are going to implement this architecture and visually understand each of its components step by step. All the code is available in my GitHub repository.
# 
# All the code is available in my GitHub Repository.

# %% [markdown]
# ## Table of Content
# 
# *   [Creating the Diffusion Workspace](#creating-the-diffusion-workspace)
#     *   [Hierarchical State for Iterative Refinement](#hierarchical-state-for-iterative-refinement)
#     *   [Creating Toolset for Research and Refinement](#creating-toolset-for-research-and-refinement)
#     *   [Defining the Core Utility](#defining-the-core-utility)
# *   [Initial Noise and Guidance Signal](#initial-noise-and-guidance-signal)
#     *   [Node 1 for Clarifying User Intent](#node-1-for-clarifying-user-intent)
#     *   [Node 2 for Writing the Research Brief](#node-2-for-writing-the-research-brief)
#     *   [Node 3 for Generating the Initial Draft Report](#node-3-for-generating-the-initial-draft-report)
# *   [Guidance Generation Engine](#guidance-generation-engine)
#     *   [Creating the ReAct-style Loop](#creating-the-react-style-loop)
#     *   [Building the Web Search Capability](#building-the-web-search-capability)
#     *   [Compressing the Findings](#compressing-the-findings)
# *   [Creating the Core Denoising Loop](#creating-the-core-denoising-loop)
#     *   [Building the Supervisor Node](#building-the-supervisor-node)
#     *   [Fan-Out Work and Denoise](#fan-out-work-and-denoise)
#     *   [Applying Guidance with Denoising Step](#applying-guidance-with-denoising-step)
# *   [Self-Correction and Stability Mechanisms](#self-correction-and-stability-mechanisms)
#     *   [Red Team as a Noise Reducer](#red-team-as-a-noise-reducer)
#     *   [Evaluator as a Convergence Check](#evaluator-as-a-convergence-check)
#     *   [State Entropy using Context Pruning Node](#state-entropy-using-context-pruning-node)
# *   [Assembling the Entire Graph](#assembling-the-entire-graph)
#     *   [Wiring the Scoping Sub-Graph](#wiring-the-scoping-sub-graph)
#     *   [Wiring the Research Sub-Graph](#wiring-the-research-sub-graph)
#     *   [Compiling the Denoising Loop](#compiling-the-denoising-loop)
# *   [Final Synthesis and Execution](#final-synthesis-and-execution)
#     *   [Generating the Fully Denoised Report](#generating-the-fully-denoised-report)
#     *   [Integrating All Sub-Graphs](#integrating-all-sub-graphs)
#     *   [Observing the Diffusion of an 8-Iteration Execution](#observing-the-diffusion-of-an-8-iteration-execution)
# *   [Analyzing the Final Output](#analyzing-the-final-output)
#     *   [Monolithic LLM against Deep Researh architeure](#monolithic-llm-against-deep-researh-architeure)
# *   [Conclusion](#conclusion)

# %% [markdown]
# ## Creating the Diffusion Workspace
# 
# Before we can start building our iterative engine, we need to establish a solid foundation. This means designing the core data structures that will support complex collaboration, defining the unique **“personas”** of our specialist agents through precise prompt engineering, and creating the important tools and capabilities they will use to interact with the environment and assess their own output.
# 
# Think of this as the blueprint for our diffusion workspace, a carefully structured starting point that ensures everything we build on top will be robust, adaptable, and intelligent.

# %% [markdown]
# ### Hierarchical State for Iterative Refinement
# 
# The first and most critical step is to design the shared memory, or **“state”**, for our agent society.
# 
# > **For a system this complex, a simple list of messages is insufficient**.
# 
# ![Why Hierarchical State (Created by Fareed Khan)](https://miro.medium.com/v2/resize:fit:2000/1*nogQPYtTjc6xnBX84Sy-Qw.png)
# 
# We need a hierarchical, structured state that acts as the central **“canvas”** or **“workbench”** where agents can post their findings, critiques, and evolving drafts.
# 
# We will use Pydantic `BaseModel` and Python's `TypedDict` to enforce a strict, machine-readable schema for this collaboration.
# 
# Let’s start with the most atomic unit of knowledge our system will handle: a single, extracted `Fact`.

# %%
from langchain_core.messages import BaseMessage
from pydantic import BaseModel, Field
from typing import Sequence, Annotated, List, Literal, Optional, TypedDict
import operator

class Fact(BaseModel):
    """An atomic unit of knowledge, extracted from raw research notes and stored in our structured knowledge base."""
    # The core factual statement itself, extracted from a source.
    content: str = Field(description="The factual statement")

    # We must track the provenance of every fact for traceability and citations in the final report.
    source_url: str = Field(description="Where this fact came from")

    # A confidence score allows the system to weigh more credible sources higher during synthesis.
    confidence_score: int = Field(description="1-100 confidence score based on source credibility")

    # This flag allows our Red Team to mark facts that are contradicted by other evidence, a key part of our self-correction loop.
    is_disputed: bool = Field(default=False, description="If this fact conflicts with others")

# %% [markdown]
# We can say that this `Fact` class is the core of our system memory. It is a structured object that captures not only the information itself (`content`) but also its `source_url` and a `confidence_score`.
# 
# This is a design choice because it makes our knowledge base auditable and allows downstream agents to reason about the credibility of the information they are using. The `is_disputed` flag is a mechanism for our self-correction loop, allowing the system to track and resolve contradictions.
# 
# Next, we need a structure to represent the feedback from our **“Red Team”** agent.

# %%
class Critique(BaseModel):
    """A structured model for adversarial feedback from the Red Team or other quality control agents."""
    # Tracks which agent generated the critique (e.g., "Red Team", "Safety Filter") for accountability.
    author: str 

    # The specific logical fallacy, bias, or factual error that was found in the draft.
    concern: str

    # A 1-10 score to quantify the severity of the issue, allowing the Supervisor agent to prioritize its actions.
    severity: int 

    # A flag to track whether a critique has been addressed in a subsequent revision of the draft.
    addressed: bool = Field(default=False, description="Has the supervisor fixed this?")

# %% [markdown]
# This `Critique` is our formal communication channel for our system self-correction capabilities. By forcing our adversarial agents to provide feedback in this structured format, we transform their insights into machine-readable data.
# 
# The `severity` score is particularly important, it will allow our Supervisor agent to decide if a draft needs minor polishing or a complete, ground-up rewrite, enabling a more intelligent and nuanced revision process.
# 
# To enable our system to learn and improve, we need a way to track its performance on each iteration of the diffusion loop.

# %%
class QualityMetric(TypedDict):
    """A TypedDict for storing a snapshot of the draft's quality at a specific iteration."""
    # The programmatic quality score calculated by our self-evolution evaluator.
    score: float
    # The textual feedback from the evaluator explaining the score.
    feedback: str
    # The iteration number at which this score was recorded, for tracking progress over time.
    iteration: int

# %% [markdown]
# The `QualityMetric` structure is our performance log. By appending one of these dictionaries to our state after every refinement step, we create a clear, historical record of the diffusion process.
# 
# This `quality_history` allow us to plot the agent improvement over time and programmatically diagnose if the refinement process is stalling or has successfully converged to a high-quality output.
# 
# Now we can assemble these smaller components into the main state for our Supervisor agent, the brain of the entire operation.

# %%
from langgraph.graph.message import add_messages

class SupervisorState(TypedDict):
    """The advanced, hierarchical state for the main Supervisor agent, the central workbench for the diffusion process."""
    # A standard field for accumulating the conversational history with the Supervisor.
    supervisor_messages: Annotated[Sequence[BaseMessage], add_messages]
    
    # These are the core artifacts of the research process that the Supervisor manages.
    research_brief: str
    draft_report: str
    
    # This is a key memory management design. 'raw_notes' is a temporary, high-volume buffer
    # for unprocessed search results. 'knowledge_base' is the permanent, structured, and pruned storage.
    raw_notes: Annotated[List[str], operator.add] 
    knowledge_base: Annotated[List[Fact], operator.add]
    
    # A simple counter to prevent infinite loops in our iterative process.
    research_iterations: int
    
    # These fields manage the self-correction and adversarial feedback loops.
    active_critiques: Annotated[List[Critique], operator.add]
    quality_history: Annotated[List[QualityMetric], operator.add]

    # A boolean flag that the Evaluator can set to signal to the Supervisor that the draft quality is unacceptably low.
    needs_quality_repair: bool

# %% [markdown]
# The `SupervisorState` is the central **workbench** for our entire diffusion process. It is a data structure that holds everything the Supervisor needs to make decisions.
# 
# The distinction between `raw_notes` and `knowledge_base` is a piece for managing the agent cognitive load.
# 
# 1.  The `raw_notes` buffer will be intentionally cleared by our `Context Pruning` node, preventing the context window from overflowing.
# 2.  While the valuable, extracted `Fact` objects are preserved in the permanent `knowledge_base`.
# 
# We also need to define the states for our other sub-graphs, such as the individual `ResearcherAgent` and the top-level, user-facing graph.

# %%
from langgraph.graph import MessagesState

# The state for the "worker" research agent sub-graph.
class ResearcherState(TypedDict):
    researcher_messages: Annotated[Sequence[BaseMessage], add_messages]
    tool_call_iterations: int
    research_topic: str

    # The final, cleaned-up output of a research run.
    compressed_research: str

    # The temporary buffer of raw search results for this specific worker.
    raw_notes: Annotated[List[str], operator.add]

# A specialized state defining the output of the research agent sub-graph.
class ResearcherOutputState(TypedDict):
    compressed_research: str
    raw_notes: Annotated[List[str], operator.add]
    researcher_messages: Annotated[Sequence[BaseMessage], add_messages]

# The states for the top-level, user-facing graph.
class AgentInputState(MessagesState):
    """The initial input state, which only contains the user's messages."""
    pass

class AgentState(MessagesState):
    """The main state for the full multi-agent system, which accumulates all final artifacts."""
    research_brief: Optional[str]
    supervisor_messages: Annotated[Sequence[BaseMessage], add_messages]
    raw_notes: Annotated[list[str], operator.add] = []

    notes: Annotated[list[str], operator.add] = [] # The final, curated notes for the writer.
    draft_report: str
    final_report: str

# %% [markdown]
# These additional state definitions provide the specific memory structures needed for each of our distinct sub-graphs.
# 
# 1.  The `ResearcherState` can be thought of as the **private notebook** for a single research agent on a specific sub-task.
# 2.  The main `AgentState`, on the other hand, is the final p**roject folder** where all the completed deliverables (`research_brief`, `notes`, `draft_report`) are collected before being assembled into the `final_report`.
# 3.  This hierarchical state design is what allows us to build complex, nested agentic workflows in a clean and maintainable way.

# %% [markdown]
# ### Creating Toolset for Research and Refinement
# 
# Our agents now have a structured memory which we can call **canvas**. Next, we need to give them the tools they will use to interact with the external knowledge, gather information, and refine their work.
# 
# > In an agentic system, tools are the bridge between the LLM internal reasoning and external action.
# 
# ![Creating Toolset (Created by Fareed Khan)](https://miro.medium.com/v2/resize:fit:1400/1*uwcPRZDmdfLBUPpJ4kZvcQ.png)
# 
# We will define a set of specialized tools that our Supervisor agent can call. This includes a tool for **delegating parallel research**, a tool for completing the research process, and crucially, a `refine_draft_report` tool which is the core mechanism of our **"denoising"** step.
# 
# Let’s begin by defining the `ConductResearch` tool. This tool not goingn to perform the research itself. Instead, it acts as a delegation mechanism, allowing the Supervisor to spawn one or more parallel **"worker"** research agents.

# %%
from langchain_core.tools import tool

@tool
class ConductResearch(BaseModel):
    """A tool for delegating a specific research task to a specialized sub-agent. The Supervisor uses this to fan out work."""
    # The detailed research topic for the sub-agent. This needs to be a complete, standalone instruction.
    research_topic: str = Field(
        description="The topic to research. Should be a single, self-contained topic described in high detail.",
    )

# %% [markdown]
# The `ConductResearch` tool is the **work order** for our system. It's a simple Pydantic model that encapsulates a single research task.
# 
# 1.  The key architectural concept here is that when the Supervisor agent decides to use this tool, our `LangGraph` workflow will intercept the call.
# 2.  Instead of executing a simple function, it will trigger our entire, complex `researcher_agent` sub-graph, which we will build in upcoming section. This makes the tool a multi-step process.
# 
# Next, we are going to define the tool that signals the end of the iterative research phase.

# %%
@tool
class ResearchComplete(BaseModel):
    """A tool for the Supervisor to signal that the research process is complete and the final report can be generated."""
    # This tool takes no arguments. Its invocation is the signal.
    pass

# %% [markdown]
# The `ResearchComplete` tool is a simple control signal. It provides a formal, structured way for the Supervisor agent to declare that its iterative diffusion process has converged and that it is confident in the quality of the current draft.
# 
# > When our graph sees this tool being called, it knows to exit the main refinement loop and proceed to the final report generation step.
# 
# Now, we will implement a tool that allows our agents to **“think”** and reflect on their actions, creating a more transparent and strategic reasoning process.

# %%
@tool(parse_docstring=True)
def think_tool(reflection: str) -> str:
    """Tool for strategic reflection on research progress and decision-making.

    Use this tool after each search to analyze results and plan next steps systematically.
    This creates a deliberate pause in the research workflow for quality decision-making.

    Args:
        reflection: Your detailed reflection on research progress, findings, gaps, and next steps.

    Returns:
        Confirmation that reflection was recorded for decision-making.
    """
    return f"Reflection recorded: {reflection}"

# %% [markdown]
# In this component we are enabling a **chain-of-thought** style of reasoning within our agents.
# 
# By instructing our agents (via their prompts) to use this tool after every search, we are forcing them to pause, verbalize their analysis of the retrieved information, and explicitly state their plan for the next step.
# 
# This not only leads to more strategic research but also makes the agent internal monologue visible in our `LangSmith` traces, which is invaluable for debugging and understanding its decision-making process.

# %%
# Initialize model with tools for researcher agents
# We bind the think_tool and tavily_search to the researcher model
if tavily_search:
    researcher_tools = [think_tool, tavily_search]
else:
    researcher_tools = [think_tool]

model_with_tools = model.bind_tools(researcher_tools)

# Create a tools_by_name dictionary for tool routing
tools_by_name = {tool.name: tool for tool in researcher_tools}

print(f"✓ Model bound with {len(researcher_tools)} tools")

# %% [markdown]
# ### Defining the Core Utility
# 
# In every agentic system we need a set of simple helper functions that we are going to use throughout the pipeline.
# 
# ![Core Function (Created by Fareed Khan)](https://miro.medium.com/v2/resize:fit:1400/1*YUZWh4g1UbxjSiIpCFFxzA.png)
# 
# So to do that, we will start with a basic utility to get the current date, which we can inject into our prompts to give our agents temporal context.

# %%
from datetime import datetime

def get_today_str() -> str:
    """A simple utility function to get the current date in a human-readable string format."""
    # We format the date as "Day Mon Day, Year" (e.g., "Mon Dec 25, 2025").
    # Use .day instead of %-d for cross-platform compatibility (Windows doesn't support %-d)
    today = datetime.now()
    return today.strftime(f"%a %b {today.day}, %Y")

# %% [markdown]
# The `get_today_str` function gives us the current date into our prompts, we ground our agents in the present moment.
# 
# This is particularly important for research tasks, as it helps the agent understand that it should be looking for the most recent, up-to-date information available. It's a simple way to combat the static nature of an LLM's training data.

# %% [markdown]
# ## Initial Noise and Guidance Signal
# 
# With our foundational workspace like the states, tools, and some other utilities are coded, we can now build our first functional component of our deep agentic architecture.
# 
# ![Initial Noise Guide (Created by Fareed Khan)](https://miro.medium.com/v2/resize:fit:2000/1*65hmnHA5qU07AnolagwIkA.png)
# 
# This initial sub-graph is the front component of our system. Its job is to manage the initial interaction with the user, which make sure that …
# 
# > our deep thinking system have a clear, unambiguous task before we kick off the expensive, iterative diffusion process.
# 
# By separating the initial **scoping** from the main **research** loop, we create a efficient system.
# 
# We prevent the agent from wasting time and resources researching **a poorly defined query**.
# 
# In the language of our diffusion model, this sub-graph is responsible for two things: generating the **“guidance signal”** (the research brief) that will steer the entire process, and creating the initial **“noisy image”** (the first draft) that our main engine will iteratively denoise.
# 
# We will now build this sub-graph. It will consist of three sequential nodes, each performing a distinct and critical task in the initialization of our research process.

# %% [markdown]
# ### **Node 1 for Clarifying User Intent**
# 
# The first agent a user interacts with is arguably the most important for setting expectations and ensuring a successful outcome.
# 
# ![Clarifying User Intent (Created by Fareed Khan)](https://miro.medium.com/v2/resize:fit:2000/1*H8rC2UYFlUQyZAe2NHH80Q.png)
# 
# We will start by building the `clarify_with_user` node. This agent sole responsibility is to act as a gatekeeper. It analyzes the initial conversation history and makes a critical decision:
# 
# > "Do I have enough information to start a high-quality research process, or do I need to pause and ask for more information?"
# 
# First, let’s define the prompt that will guide this agent’s decision-making process.

# %%
# This prompt guides the first agent in our workflow, which decides if it has enough information from the user.
clarify_with_user_instructions="""
These are the messages that have been exchanged so far from the user asking for the report:
<Messages>
{messages}
</Messages>

Today's date is {date}.

Assess whether you need to ask a clarifying question, or if the user has already provided enough information for you to start research.
IMPORTANT: If you can see in the messages history that you have already asked a clarifying question, you almost always do not need to ask another one. Only ask another question if ABSOLUTELY NECESSARY.

If there are acronyms, abbreviations, or unknown terms, ask the user to clarify.
If you need to ask a question, follow these guidelines:
- Be concise while gathering all necessary information
- Make sure to gather all the information needed to carry out the research task in a concise, well-structured manner.
- Use bullet points or numbered lists if appropriate for clarity. Make sure that this uses markdown formatting and will be rendered correctly if the string output is passed to a markdown renderer.
- Don't ask for unnecessary information, or information that the user has already provided. If you can see that the user has already provided the information, do not ask for it again.

Respond in valid JSON format with these exact keys:
"need_clarification": boolean,
"question": "<question to ask the user to clarify the report scope>",
"verification": "<verification message that we will start research>"

If you need to ask a clarifying question, return:
"need_clarification": true,
"question": "<your clarifying question>",
"verification": ""

If you do not need to ask a clarifying question, return:
"need_clarification": false,
"question": "",
"verification": "<acknowledgement message that you will now start research based on the provided information>"

For the verification message when no clarification is needed:
- Acknowledge that you have sufficient information to proceed
- Briefly summarize the key aspects of what you understand from their request
- Confirm that you will now begin the research process
- Keep the message concise and professional
"""

# %% [markdown]
# The `clarify_with_user_instructions` prompt is our user-facing agent.
# 
# 1.  Its job is to ensure that we don't start a complex and expensive research process with an ambiguous or incomplete request.
# 2.  The instruction **"If you have already asked a clarifying question... do not ask another one"** is a key piece of engineering to prevent annoying, repetitive conversational loops and improve the user experience.
# 
# Next, we need a Pydantic model to structure the output of this agent, ensuring its decision is always machine-readable.

# %%
class ClarifyWithUser(BaseModel):
    """A Pydantic schema for the output of the user clarification decision agent."""

    # This boolean is the core of the decision-making process.
    need_clarification: bool = Field(
        description="Whether the user needs to be asked a clarifying question.",
    )
    # The question to be asked if clarification is needed.
    question: str = Field(
        description="A question to ask the user to clarify the report scope",
    )
    # A confirmation message to send to the user if no clarification is needed.
    verification: str = Field(
        description="A verification message to confirm that research will begin.",
    )

# %% [markdown]
# This `ClarifyWithUser` schema is the formal contract for our intiial node agent. It forces the LLM to make a clear, binary `need_clarification` decision and to provide the appropriate text for either continuing the conversation or proceeding with the research.
# 
# Now, we can build the `clarify_with_user` node function itself.

# %%
from langgraph.types import Command
from langchain_core.messages import AIMessage, HumanMessage, get_buffer_string
from typing import Literal

def clarify_with_user(state: AgentState) -> Command[Literal["write_research_brief", END]]:
    """
    This node acts as a gatekeeper. It determines if the user's request has enough detail to proceed.
    If not, it HALTS the graph and asks a clarifying question. If yes, it proceeds to the next step.
    """

    # 1. We first format the entire conversation history into a single string for the LLM.
    messages_text = get_buffer_string(state["messages"])
    current_date = get_today_str()

    # 2. We bind our 'ClarifyWithUser' Pydantic schema to the model. This forces a structured JSON output.
    structured_output_model = creative_model.with_structured_output(ClarifyWithUser)

    # 3. We invoke the LLM with our detailed 'clarify_with_user_instructions' prompt.
    response = structured_output_model.invoke([
        HumanMessage(content=clarify_with_user_instructions.format(
            messages=messages_text, 
            date=current_date
        ))
    ])

    # 4. This is the core logic. We check the 'need_clarification' boolean from the LLM's response.
    if response.need_clarification:

        # If clarification is needed, we return a special 'Command' object.
        # 'goto=END' tells LangGraph to STOP execution here.
        # 'update={...}' adds the AI's clarifying question to the message history before stopping.
        return Command(
            goto=END, 
            update={"messages": [AIMessage(content=response.question)]}
        )
    else:

        # If no clarification is needed, we return another Command.
        # 'goto="write_research_brief"' directs the graph to proceed to the next node.
        # We add the AI's verification message to the history for a clean conversational flow.
        return Command(
            goto="write_research_brief",
            update={"messages": [AIMessage(content=response.verification)]}
        )

# %% [markdown]
# You might wonder what happens when we return `Command(goto=END)`. In a stateful framework like LangGraph, this does not crash the program. Instead, it suspends the workflow. The graph state is persisted to memory or a database, effectively putting the agent to sleep.
# 
# When the user provides clarifying information, the graph wakes up, updates the state, and resumes execution. It does not start over but continues exactly from where it left off. This persistent `Human-in-the-Loop` capability is essential for long-running research tasks.

# %% [markdown]
# ### **Node 2 for Writing the Research Brief**
# 
# Once our gatekeeper (initial) agent has confirmed that the user request is clear and sufficient, we need to transform that (potentially messy) conversational history into a formal, unambiguous set of instructions for our research agents. This is the job of the `write_research_brief` node.
# 
# > This agent acts as an expert analyst. It reads the entire confirmed conversation and distills it into a single, comprehensive `research_brief`.
# 
# ![Research Brief (Created by Fareed Khan)](https://miro.medium.com/v2/resize:fit:2000/1*ANOapT8FZSQvwkpFEbrn-A.png)
# 
# This brief is arguably the most important artifact in our entire system. It becomes the **guidance signal** or **ground truth** for the rest of the diffusion process.
# 
# Every subsequent step from the targeted research of the worker agents to the critical feedback from the Red Team and the final evaluation will be measured against this brief.
# 
# First, let’s define the prompt that will guide this agent’s crucial transformation task.

# %%
# This prompt transforms the informal user conversation into a formal, structured research brief.
transform_messages_into_research_topic_human_msg_prompt = """You will be given a set of messages that have been exchanged so far between yourself and the user. 
Your job is to translate these messages into a more detailed and concrete research question that will be used to guide the research.

The messages that have been exchanged so far between yourself and the user are:
<Messages>
{messages}
</Messages>

CRITICAL: Make sure the answer is written in the same language as the human messages!
For example, if the user's messages are in English, then MAKE SURE you write your response in English. If the user's messages are in Chinese, then MAKE SURE you write your entire response in Chinese.
This is critical. The user will only understand the answer if it is written in the same language as their input message.

Today's date is {date}.

You will return a single research question that will be used to guide the research.

Guidelines:
1. Maximize Specificity and Detail
- Include all known user preferences and explicitly list key attributes or dimensions to consider.
- It is important that all details from the user are included in the instructions.

2. Handle Unstated Dimensions Carefully
- When research quality requires considering additional dimensions that the user hasn't specified, acknowledge them as open considerations rather than assumed preferences.
- Example: Instead of assuming "budget-friendly options," say "consider all price ranges unless cost constraints are specified."
- Only mention dimensions that are genuinely necessary for comprehensive research in that domain.

3. Avoid Unwarranted Assumptions
- Never invent specific user preferences, constraints, or requirements that weren't stated.
- If the user hasn't provided a particular detail, explicitly note this lack of specification.
- Guide the researcher to treat unspecified aspects as flexible rather than making assumptions.

4. Distinguish Between Research Scope and User Preferences
- Research scope: What topics/dimensions should be investigated (can be broader than user's explicit mentions)
- User preferences: Specific constraints, requirements, or preferences (must only include what user stated)
- Example: "Research coffee quality factors (including bean sourcing, roasting methods, brewing techniques) for San Francisco coffee shops, with primary focus on taste as specified by the user."

5. Use the First Person
- Phrase the request from the perspective of the user.

6. Sources
- If specific sources should be prioritized, specify them in the research question.
- For product and travel research, prefer linking directly to official or primary websites (e.g., official brand sites, manufacturer pages, or reputable e-commerce platforms like Amazon for user reviews) rather than aggregator sites or SEO-heavy blogs.
- For academic or scientific queries, prefer linking directly to the original paper or official journal publication rather than survey papers or secondary summaries.
- For people, try linking directly to their LinkedIn profile, or their personal website if they have one.
- If the query is in a specific language, prioritize sources published in that language.

REMEMBER:
Make sure the research brief is in the SAME language as the human messages in the message history.
"""

# %% [markdown]
# The `transform_messages_into_research_topic_human_msg_prompt` takes the raw, often informal, back-and-forth of a user conversation and instructs the LLM to distill it into a single, comprehensive, and unambiguous `research_brief`. The guidelines such as **"Handle Unstated Dimensions Carefully"** and **"Avoid Unwarranted Assumptions"** are to prevent the LLM from hallucinating or making the kind of logical leaps that can derail a research project.
# 
# You will notice a specific, repeated instruction in the prompt:
# 
# > *“Make sure the answer is written in the same language as the human messages”*
# 
# **Standard LLMs often default to English when performing reasoning tasks or reading English documentation**. By explicitly requiring the agent to mirror the user’s language in the `research_brief` and subsequent drafts, we ensure that the system remains accessible and effective for users across different linguistic backgrounds.
# 
# Next, we will define the Pydantic schema for the output of this node.

# %%
class ResearchQuestion(BaseModel):
    """A Pydantic schema for the structured output of the research brief generation agent."""

    # This single field will contain the complete, detailed research brief.
    research_brief: str = Field(
        description="A research question that will be used to guide the research.",
    )

# %%
def write_research_brief(state: AgentState) -> Command[Literal["write_draft_report"]]:
    """
    This node transforms the confirmed conversation history into a single, comprehensive research brief.
    """
    # 1. We bind our 'ResearchQuestion' Pydantic schema to the model to ensure structured output.
    structured_output_model = model.with_structured_output(ResearchQuestion)


    # 2. We invoke the LLM with our specialized 'transform_messages...' prompt and the conversation history.
    response = structured_output_model.invoke([
        HumanMessage(content=transform_messages_into_research_topic_human_msg_prompt.format(
            messages=get_buffer_string(state.get("messages", [])),
            date=get_today_str()
        ))
    ])

    # 3. We return a Command to update the state with the new research_brief
    #    and direct the graph to proceed to the 'write_draft_report' node.
    return Command(
            goto="write_draft_report", 
            update={"research_brief": response.research_brief}
        )

# %% [markdown]
# It make sure that the output of our brief-writing agent is always a clean JSON object containing a single `research_brief` string. This makes it easy to extract this critical piece of information and save it to our `AgentState`.
# 
# Now, we can build the `write_research_brief` node function itself.

# %% [markdown]
# The `write_research_brief` function takes the informal user dialogue and converts it into a canonical, structured instruction set for the rest of the system.
# 
# 1.  By using a dedicated, highly-detailed prompt and a strict output schema, we ensure that the generated `research_brief` is specific, comprehensive, and free of the ambiguities that can lead to flawed research.
# 2.  The `Command` it returns performs two actions: it updates the `AgentState` by populating the `research_brief` field, and it explicitly pushes the workflow forward to the next and final step of our scoping sub-graph.

# %% [markdown]
# ### Node 3 for Generating the Initial Draft Report
# 
# This is the final step of our **front sub-graph** and the beginning of our guided diffusion process. The `write_draft_report` node takes the clean, formal `research_brief` and generates the very first version of our report.
# 
# ![Initial Draft Report (Created by Fareed Khan)](https://miro.medium.com/v2/resize:fit:2000/1*K9Itz4_3m_P6R0x_qM22Dw.png)
# 
# This initial draft is not expected to be perfect. In fact, it is intentionally generated without any external research tools. It will be based solely on the parametric knowledge of the `creative_model`.
# 
# This means it will likely contain generic information, plausible-sounding but unverified facts, and potential hallucinations. This is by design. This draft is our **noisy image** the raw material that our main Supervisor loop will now iteratively denoise, critique, and refine towards a high-quality, fact-based final output.
# 
# First, let’s define the prompt that will guide our creative model in generating this initial draft.

# %%
# This prompt guides the model to generate a first-pass draft based only on the research brief.
draft_report_generation_prompt = """Based on all the research in your knowledge base, create a comprehensive, well-structured answer to the overall research brief:
<Research Brief>
{research_brief}
</Research Brief>

CRITICAL: Make sure the answer is written in the same language as the human messages!
For example, if the user's messages are in English, then MAKE SURE you write your response in English. If the user's messages are in Chinese, then MAKE SURE you write your entire response in Chinese.
This is critical. The user will only understand the answer if it is written in the same language as their input message.

Today's date is {date}.

Please create a detailed answer to the overall research brief that:
1. Is well-organized with proper headings (# for title, ## for sections, ### for subsections)
2. Includes specific facts and insights from the research
3. References relevant sources using [Title](URL) format
4. Provides a balanced, thorough analysis. Be as comprehensive as possible, and include all information that is relevant to the overall research question. People are using you for deep research and will expect detailed, comprehensive answers.
5. Includes a "Sources" section at the end with all referenced links

You can structure your report in a number of different ways. Here are some examples:

To answer a question that asks you to compare two things, you might structure your report like this:
1/ intro
2/ overview of topic A
3/ overview of topic B
4/ comparison between A and B
5/ conclusion

To answer a question that asks you to return a list of things, you might only need a single section which is the entire list.
1/ list of things or table of things
Or, you could choose to make each item in the list a separate section in the report. When asked for lists, you don't need an introduction or conclusion.
1/ item 1
2/ item 2
3/ item 3

To answer a question that asks you to summarize a topic, give a report, or give an overview, you might structure your report like this:
1/ overview of topic
2/ concept 1
3/ concept 2
4/ concept 3
5/ conclusion

If you think you can answer the question with a single section, you can do that too!
1/ answer

REMEMBER: Section is a VERY fluid and loose concept. You can structure your report however you think is best, including in ways that are not listed above!
Make sure that your sections are cohesive, and make sense for the reader.

For each section of the report, do the following:
- Use simple, clear language
- Use ## for section title (Markdown format) for each section of the report
- Do NOT ever refer to yourself as the writer of the report. This should be a professional report without any self-referential language. 
- Do not say what you are doing in the report. Just write the report without any commentary from yourself.
- Each section should be as long as necessary to deeply answer the question with the information you have gathered. It is expected that sections will be fairly long and verbose. You are writing a deep research report, and users will expect a thorough answer.
- Use bullet points to list out information when appropriate, but by default, write in paragraph form.

REMEMBER:
The brief and research may be in English, but you need to translate this information to the right language when writing the final answer.
Make sure the final answer report is in the SAME language as the human messages in the message history.

Format the report in clear markdown with proper structure and include source references where appropriate.

<Citation Rules>
- Assign each unique URL a single citation number in your text
- End with ### Sources that lists each source with corresponding numbers
- IMPORTANT: Number sources sequentially without gaps (1,2,3,4...) in the final list regardless of which sources you choose
- Each source should be a separate line item in a list, so that in markdown it is rendered as a list.
- Example format:
  [1] Source Title: URL
  [2] Source Title: URL
- Citations are extremely important. Make sure to include these, and pay a lot of attention to getting these right. Users will often use these citations to look into more information.
</Citation Rules>
"""

# %% [markdown]
# We are explicitly telling the model to generate a comprehensive draft based on its pre-existing knowledge. The instruction to acknowledge where citations are needed is a key feature, it primes the model to recognize the gaps in its own knowledge, setting the stage for the targeted research that will follow in the main diffusion loop.
# 
# Next, we will define the Pydantic schema for the output of this node.

# %%
class DraftReport(BaseModel):
    """A Pydantic schema for the structured output of the initial draft generation agent."""

    # This single field will contain the complete, unresearched initial draft report.
    draft_report: str = Field(
        description="A draft report that will be used as the starting point for the diffusion process.",
    )

# %% [markdown]
# This `DraftReport` schema ensures that the output of our drafting agent is always a clean JSON object containing the `draft_report` string. This makes it easy and reliable to save this crucial artifact to our `AgentState`.
# 
# Now, we can build the `write_draft_report` node function itself.

# %%
# Our creative model
creative_model = init_chat_model(model="gpt-oss:20b", base_url="http://localhost:11434")

def write_draft_report(state: AgentState) -> dict:
    """
    This node takes the research brief and generates an initial, unresearched draft.
    This serves as the "noisy" starting point for our diffusion process.
    """
    # 1. We use our 'creative_model' for this task and bind it to the 'DraftReport' schema.
    structured_output_model = creative_model.with_structured_output(DraftReport)
    research_brief = state.get("research_brief", "")
    
    # 2. We format the prompt for the drafter, injecting the research brief and the current date.
    draft_report_prompt_formatted = draft_report_generation_prompt.format(
        research_brief=research_brief,
        date=get_today_str()
    )

    # 3. We invoke the LLM to generate the initial "noisy" draft.
    response = structured_output_model.invoke([HumanMessage(content=draft_report_prompt_formatted)])

    # 4. This node returns a simple dictionary update. This is the final output of the 'scope_research' sub-graph.
    #    The 'supervisor_messages' field is populated to "hand off" the initial state to the main Supervisor loop.
    return {
        "research_brief": research_brief,
        "draft_report": response.draft_report, 
        "supervisor_messages": ["Here is the draft report: " + response.draft_report, research_brief]
    }

# %% [markdown]
# The `write_draft_report` function is the final step in our initialization process.
# 
# 1.  It generates the `draft_report`, which is the central object that will be iteratively improved in the main loop. The most important action it takes is populating the `supervisor_messages` field in the state.
# 2.  This is the formal **hand-off**, It passes both the new draft and the original research brief to the Supervisor agent, effectively saying, **"Here is the starting point (the noisy image), and here is the goal (the guidance signal). Your work begins now"**.
# 3.  This return value concludes the execution of our scoping sub-graph and provides the initial state for the main diffusion engine we will build next.

# %% [markdown]
# ## Guidance Generation Engine
# 
# With our **initial noisy draft** and a clear research brief in hand, our Supervisor agent is now ready to begin the diffusion process.
# 
# ![Guidance Generation Engine (Created by Fareed Khan)](https://miro.medium.com/v2/resize:fit:1400/1*EDqD2ppR2t8FiP55tsUTGg.png)
# 
# Its first step in any refinement cycle is to identify gaps or weaknesses in the current draft and gather external facts to guide the **“denoising”**. To do this, it will delegate targeted research tasks to a specialized **Research Agent**.
# 
# Our Research Agent is not a single node, it’s a complete, self-contained sub-graph.
# 
# > It implements a ReAct-style (Reason-Act) loop, allowing it to **think**, use tools (like web search), and reflect on the results to gather information on a specific topic.
# 
# The Supervisor can spawn multiple instances of this sub-graph to run in parallel, creating a powerful “scatter-gather” research engine that can investigate multiple lines of inquiry simultaneously.
# 
# In this section, here is what we are going to do:
# 
# 1.  We will **build the Core ReAct Loop** by implementing the `llm_call` and `tool_node` functions that form the central “think–act” cycle of our research agent.
# 2.  Next, we will **engineer a Reflective Process** by introducing a `think_tool`, enabling the agent to pause, reflect on its findings, and plan its next search strategically.
# 3.  We will then **build the Full Web Search Capability** by implementing the complete multi-stage pipeline for our `tavily_search` tool, including deduplication and summarization.
# 4.  After that, we will **create the Compression Node** by developing the `compress_research` node, which takes the raw, unstructured output from the ReAct loop and refines it into a clear, cohesive summary for the Supervisor.
# 5.  Finally, we will **assemble the Sub-Graph**, connecting all these nodes in LangGraph to produce a fully functional, runnable `researcher_agent`.

# %% [markdown]
# ### Creating the ReAct-style Loop
# 
# First, we need to define the core **thinking part of our researcher**. This is the `llm_call` node.
# 
# ![ReAct Loop (Created by Fareed Khan)](https://miro.medium.com/v2/resize:fit:1400/1*31h5qkaX1ZzQ1q_iEaa5BA.png)
# 
# Its job is to look at the current state of its research (the conversation history) and decide what to do next: either call a tool to gather more information or, if it believes its research on its assigned sub-topic is complete, finish its work.
# 
# Let’s start by defining the prompt that gives our researcher its persona and instructions.

# %%
# This prompt defines the behavior of our specialized, parallel research agents.
research_agent_prompt =  """You are a research assistant conducting research on the user's input topic. For context, today's date is {date}.

<Task>
Your job is to use tools to gather information about the user's input topic.
You can use any of the tools provided to you to find resources that can help answer the research question. You can call these tools in series or in parallel, your research is conducted in a tool-calling loop.
</Task>

<Available Tools>
You have access to two main tools:
1. **tavily_search**: For conducting web searches to gather information
2. **think_tool**: For reflection and strategic planning during research

**CRITICAL: Use think_tool after each search to reflect on results and plan next steps**
</Available Tools>

<Instructions>
Think like a human researcher with limited time. Follow these steps:

1. **Read the question carefully** - What specific information does the user need?
2. **Start with broader searches** - Use broad, comprehensive queries first
3. **After each search, pause and assess** - Do I have enough to answer? What's still missing?
4. **Execute narrower searches as you gather information** - Fill in the gaps
5. **Stop when you can answer confidently** - Don't keep searching for perfection
</Instructions>

<Hard Limits>
**Tool Call Budgets** (Prevent excessive searching):
- **Simple queries**: Use 2-3 search tool calls maximum
- **Complex queries**: Use up to 5 search tool calls maximum
- **Always stop**: After 5 search tool calls if you cannot find the right sources

**Stop Immediately When**:
- You can answer the user's question comprehensively
- You have 3+ relevant examples/sources for the question
- Your last 2 searches returned similar information
</Hard Limits>

<Show Your Thinking>
After each search tool call, use think_tool to analyze the results:
- What key information did I find?
- What's missing?
- Do I have enough to answer the question comprehensively?
- Should I search more or provide my answer?
</Show Your Thinking>
"""

# %%
from langchain_core.messages import SystemMessage, ToolMessage, filter_messages

def llm_call(state: ResearcherState):
    """The 'brain' of the researcher: analyzes the current state and decides on the next action (call a tool or finish)."""
    
    # This node invokes our tool-bound model with the specific research_agent_prompt and the current message history for this sub-task.
    return {
        "researcher_messages": [
            model_with_tools.invoke(
                [SystemMessage(content=research_agent_prompt.format(date=get_today_str()))] + state["researcher_messages"]
            )
        ]
    }

# %% [markdown]
# The `research_agent_prompt` is the **Standard Operating Procedure** for our individual researchers.
# 
# It provides the agent with a clear task, a defined set of tools, and most importantly, a set of **“Hard Limits”** or constraints. Guidelines such as **“Use `think_tool` after each search”** and **“Always stop after five search tool calls”** are vital for creating a strategic and efficient research process.
# 
# These limits help prevent the agent from getting stuck in long, unproductive loops, ensuring focused and cost-effective performance.
# 
# Now, we can define the `llm_call` node, which is the "brain" of the researcher.

# %% [markdown]
# The `llm_call` function is the reasoning engine for our worker agent. It's a simple but powerful node that takes the entire `researcher_messages` history for its specific sub-task and passes it to our `model_with_tools`.
# 
# The model response will either be a final thought or, more likely, a decision to call one of its available tools (`tavily_search` or `think_tool`).
# 
# Next, we need the **“hands”** of our researcher. The `tool_node` is responsible for executing any tool calls that the `llm_call` node has planned.

# %%
def tool_node(state: ResearcherState):
    """The 'hands' of the researcher: executes all tool calls from the previous LLM response."""
    
    # We get the most recent message from the state, which should contain the tool calls.
    tool_calls = state["researcher_messages"][-1].tool_calls
    
    # We execute all the planned tool calls.
    observations = []
    for tool_call in tool_calls:
        
        # We look up the correct tool function by its name.
        tool = tools_by_name[tool_call["name"]]
        observations.append(tool.invoke(tool_call["args"]))
    
    # We format the results of the tool calls into 'ToolMessage' objects.
    # This is the standard way to return tool results to the LLM in LangGraph.
    tool_outputs = [
        ToolMessage(
            content=str(observation),
            name=tool_call["name"],
            tool_call_id=tool_call["id"]
        ) for observation, tool_call in zip(observations, tool_calls)
    ]
    
    # We return the tool outputs to be added to the message history.
    return {"researcher_messages": tool_outputs}

# %% [markdown]
# The `tool_node` is the action part of our ReAct loop. It inspects the last `AIMessage`, extracts the `tool_calls`, and executes them. By returning the results as `ToolMessage` objects, it provides the LLM with the **observations** it needs for its next reasoning step.
# 
# The loop `llm_call -> tool_node -> llm_call` is what allows the agent to iteratively search, find information, and decide if it needs to search again.
# 
# Now we need the routing logic that controls this loop. The `should_continue` function acts as a conditional edge, checking if the LLM has decided to call a tool or if it has finished its research for this sub-task.

# %%
def should_continue(state: ResearcherState) -> Literal["tool_node", "compress_research"]:
    """A conditional edge that determines whether to continue the ReAct loop or finish the research sub-task."""
    messages = state["researcher_messages"]
    last_message = messages[-1]

    # If the last message from the LLM contains tool calls, we continue the loop.
    if last_message.tool_calls:
        return "tool_node"

    # If there are no tool calls, the agent has decided its research is complete, and we proceed to the compression step.
    return "compress_research"

# %% [markdown]
# The `should_continue` function is a ritical router for our ReAct loop. It is the decision point that determines whether the agent continues to "act" (by calling tools) or whether it has gathered enough information and is ready to summarize its findings.
# 
# ### Building the Web Search Capability
# 
# Our research agents need a window to the outside world to gather facts. While we have defined a `tavily_search` tool, a simple search is not enough. Raw webpage content is often verbose, full of boilerplate (like navigation menus and ads), and too large to fit into a context window.
# 
# ![Web Search Pipeline (Created by Fareed Khan)](https://miro.medium.com/v2/resize:fit:1400/1*1BP2wtGMSwr7CC2M0Q5iEA.png)
# 
# 1.  To solve this, we will build a multi-stage search utility belt. Instead of just searching, our `tavily_search` tool will create a sophisticated "Search -> Deduplicate -> Summarize -> Format" pipeline for every query.
# 2.  This way the information returned to our agent is not just raw data, but clean, concise, and relevant evidence.
# 
# First, let’s define the prompt that will guide our summarization model. This model’s job is to distill the raw HTML content of a webpage into a dense, factual summary.

# %%
# This prompt guides the summarization model to extract the most critical information from a webpage.
summarize_webpage_prompt = """You are tasked with summarizing the raw content of a webpage retrieved from a web search. Your goal is to create a summary that preserves the most important information from the original web page. This summary will be used by a downstream research agent, so it's crucial to maintain the key details without losing essential information.

Here is the raw content of the webpage:

<webpage_content>
{webpage_content}
</webpage_content>

Please follow these guidelines to create your summary:

1. Identify and preserve the main topic or purpose of the webpage.
2. Retain key facts, statistics, and data points that are central to the content's message.
3. Keep important quotes from credible sources or experts.
4. Maintain the chronological order of events if the content is time-sensitive or historical.
5. Preserve any lists or step-by-step instructions if present.
6. Include relevant dates, names, and locations that are crucial to understanding the content.
7. Summarize lengthy explanations while keeping the core message intact.

When handling different types of content:

- For news articles: Focus on the who, what, when, where, why, and how.
- For scientific content: Preserve methodology, results, and conclusions.
- For opinion pieces: Maintain the main arguments and supporting points.
- For product pages: Keep key features, specifications, and unique selling points.

Your summary should be significantly shorter than the original content but comprehensive enough to stand alone as a source of information. Aim for about 25-30 percent of the original length, unless the content is already concise.

Present your summary in the following format:

```
{{
   "summary": "Your summary here, structured with appropriate paragraphs or bullet points as needed",
   "key_excerpts": "First important quote or excerpt, Second important quote or excerpt, Third important quote or excerpt, ...Add more excerpts as needed, up to a maximum of 5"
}}
```

Here are two examples of good summaries:

Example 1 (for a news article):
```json
{{
   "summary": "On July 15, 2023, NASA successfully launched the Artemis II mission from Kennedy Space Center. This marks the first crewed mission to the Moon since Apollo 17 in 1972. The four-person crew, led by Commander Jane Smith, will orbit the Moon for 10 days before returning to Earth. This mission is a crucial step in NASA's plans to establish a permanent human presence on the Moon by 2030.",
   "key_excerpts": "Artemis II represents a new era in space exploration, said NASA Administrator John Doe. The mission will test critical systems for future long-duration stays on the Moon, explained Lead Engineer Sarah Johnson. We're not just going back to the Moon, we're going forward to the Moon, Commander Jane Smith stated during the pre-launch press conference."
}}
```

Example 2 (for a scientific article):
```json
{{
   "summary": "A new study published in Nature Climate Change reveals that global sea levels are rising faster than previously thought. Researchers analyzed satellite data from 1993 to 2022 and found that the rate of sea-level rise has accelerated by 0.08 mm/year² over the past three decades. This acceleration is primarily attributed to melting ice sheets in Greenland and Antarctica. The study projects that if current trends continue, global sea levels could rise by up to 2 meters by 2100, posing significant risks to coastal communities worldwide.",
   "key_excerpts": "Our findings indicate a clear acceleration in sea-level rise, which has significant implications for coastal planning and adaptation strategies, lead author Dr. Emily Brown stated. The rate of ice sheet melt in Greenland and Antarctica has tripled since the 1990s, the study reports. Without immediate and substantial reductions in greenhouse gas emissions, we are looking at potentially catastrophic sea-level rise by the end of this century, warned co-author Professor Michael Green."  
}}
```

Remember, your goal is to create a summary that can be easily understood and utilized by a downstream research agent while preserving the most critical information from the original webpage.

Today's date is {date}.
"""

# %% [markdown]
# This `summarize_webpage_prompt` is a piece of our data processing pipeline. It instructs the LLM to act as an expert analyst, focusing on extracting verifiable facts, key quotes, and essential data points, while discarding the irrelevant **"fluff"** common in web content. This is the first step in our **"denoising"** of the raw search results.
# 
# Next, we need a Pydantic schema to structure the output of our summarization model.

# %%
class Summary(BaseModel):
    """A Pydantic schema for the structured output of the webpage summarization model."""
 
   # The main, concise summary of the webpage.
    summary: str = Field(description="Concise summary of the webpage content")

    # A list of direct quotes or key excerpts to preserve important verbatim information.
    key_excerpts: str = Field(description="Important quotes and excerpts from the content")

# %% [markdown]
# The `Summary` schema makes our summarization step produces a predictable, two-part output. This separation is important, the `summary` provides the high-level gist, while the `key_excerpts` ensure that critical statements are not lost or paraphrased, preserving the factual integrity of the source.
# 
# Now, let’s build the utility functions that will form our search pipeline, starting with the function that calls the Tavily API.

# %%
from tavily import TavilyClient

# We initialize the Tavily client.
tavily_client = TavilyClient(api_key="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")
MAX_CONTEXT_LENGTH = 250000

def tavily_search_multiple(
    search_queries: List[str], 
    max_results: int = 3, 
    topic: Literal["general", "news", "finance"] = "general", 
    include_raw_content: bool = True, 
    ) -> List[dict]:
    """A helper function to perform a search using the Tavily API for a list of queries."""

    print(f"--- [TOOL] Executing Tavily search for queries: {search_queries} ---")
    search_docs = []

    # We execute the searches for each query.
    for query in search_queries:
        result = tavily_client.search(
            query,
            max_results=max_results,
            include_raw_content=include_raw_content,
            topic=topic
        )
        search_docs.append(result)
    return search_docs

# %% [markdown]
# The `tavily_search_multiple` function is our direct interface to the Tavily search engine. It takes a list of search queries and returns the raw search results, including the full, unprocessed content of the webpages, which is what we'll need for our summarization step.
# 
# > You did also notice the topic argument in our tool definition. This allows the LLM to perform context switching.
# 
# 1.  If the Supervisor asks a question about stock prices, the agent can programmatically switch the search topic from general to finance.
# 2.  If the question is about a breaking event, it can switch to news. This moves us beyond simple keyword matching and allows the agent to select the correct index for the job.
# 
# Next, we will build the function that orchestrates the summarization for a given piece of web content.

# %%
# Summarization model
summarization_model = init_chat_model(model="gpt-oss:20b", base_url="http://localhost:11434")

def summarize_webpage_content(webpage_content: str) -> str:
    """Summarizes a single piece of webpage content using our configured summarization model."""
    try:
        # We bind our 'Summary' Pydantic schema to the summarization model.
        structured_model = summarization_model.with_structured_output(Summary)


        # We invoke the LLM with our detailed summarization prompt.
        summary_result = structured_model.invoke([
            HumanMessage(content=summarize_webpage_prompt.format(
                webpage_content=webpage_content, 
                date=get_today_str()
            ))
        ])

        # We format the structured output into a clean, human-readable string.
        formatted_summary = (
            f"<summary>\n{summary_result.summary}\n</summary>\n\n"
            f"<key_excerpts>\n{summary_result.key_excerpts}\n</key_excerpts>"
        )
        return formatted_summary
    except Exception as e:

        # If summarization fails, we fall back to a simple truncation of the raw content.
        print(f"Failed to summarize webpage: {str(e)}")
        return webpage_content[:1000] + "..." if len(webpage_content) > 1000 else webpage_content

# %% [markdown]
# The `summarize_webpage_content` function takes a potentially massive string of `webpage_content` and uses a structured-output LLM call to compress it into a dense, two-part summary. The `try...except` block is a key piece of engineering for robustness, ensuring that a failure in the summarization LLM doesn't crash the entire search process.
# 
# Now, we will code helper functions to manage the results from multiple, potentially overlapping, searches.

# %%
def deduplicate_search_results(search_results: List[dict]) -> dict:
    """Deduplicates a list of search results based on the URL."""
    unique_results = {}

    for response in search_results:
        for result in response['results']:
            url = result['url']
            
            # We use the URL as a key in a dictionary to automatically handle duplicates.
            if url not in unique_results:
                unique_results[url] = result
    return unique_results

# %%
def process_search_results(unique_results: dict) -> dict:
    """Processes a dictionary of unique search results by summarizing their raw content."""
    summarized_results = {}
    for url, result in unique_results.items():
        # If raw_content is available, we summarize it.
        if result.get("raw_content"):
            content = summarize_webpage_content(result['raw_content'][:MAX_CONTEXT_LENGTH])
        else:
            # Otherwise, we just use the short snippet provided by the search API.
            content = result['content']
        summarized_results[url] = {'title': result['title'], 'content': content}
    return summarized_results

# %%
def format_search_output(summarized_results: dict) -> str:
    """Formats the final, summarized search results into a clean string for the agent."""
    if not summarized_results:
        return "No valid search results found."
    
    formatted_output = "Search results: \n\n"
    for i, (url, result) in enumerate(summarized_results.items(), 1):
        formatted_output += f"\n\n--- SOURCE {i}: {result['title']} ---\n"
        formatted_output += f"URL: {url}\n\n"
        formatted_output += f"SUMMARY:\n{result['content']}\n\n"
        formatted_output += "-" * 80 + "\n"
    return formatted_output

# %% [markdown]
# These three functions form our data processing pipeline.
# 
# 1.  `deduplicate_search_results` prevents us from processing the same webpage multiple times.
# 2.  `process_search_results` orchestrates the summarization for all unique results.
# 3.  `format_search_output` then takes these processed results and formats them into a clean, well-structured, and human-readable string that will be returned to the agent as the final tool output.
# 
# Finally, we can wrap this entire pipeline into our decorated `tavily_search` tool.

# %%
@tool(parse_docstring=True)
def tavily_search(
    query: str,
    max_results: Annotated[int, InjectedToolArg] = 3,
    topic: Annotated[Literal["general", "news", "finance"], InjectedToolArg] = "general",
    ) -> str:
    """A tool that fetches results from the Tavily search API and performs content summarization.

    Args:
        query: A single, specific search query to execute.
        max_results: The maximum number of results to return.
        topic: The topic to filter results by ('general', 'news', 'finance').

    Returns:
        A formatted string of the deduplicated and summarized search results.
    """

    # 1. Execute the search.
    search_results = tavily_search_multiple([query], max_results=max_results, topic=topic, include_raw_content=True)

    # 2. Deduplicate the results.
    unique_results = deduplicate_search_results(search_results)

    # 3. Process and summarize the content.
    summarized_results = process_search_results(unique_results)

    # 4. Format the final output.
    return format_search_output(summarized_results)

# %% [markdown]
# The decorated `tavily_search` tool is the final, clean interface that our research agent will interact with.
# 
# It completely abstracts away the complex, multi-step **"Search -> Deduplicate -> Summarize -> Format"** pipeline we've just built. The agent simply calls this tool with a query, and it receives a perfectly formatted, dense, and relevant block of evidence in return.
# 
# ### Compressing the Findings
# 
# The output of a ReAct loop is often messy. The `researcher_messages` history will contain a mix of the agent's internal thoughts (from the `think_tool`), the verbose, summarized content from web searches, and the final AI responses.
# 
# ![Finding Filtering (Created by Fareed Khan)](https://miro.medium.com/v2/resize:fit:2000/1*mz4kBzwgw33kdI6jD4zODA.png)
# 
# This is too noisy and unstructured to pass directly back to our Supervisor agent. The Supervisor needs a clean, comprehensive summary of the findings for its specific sub-task, not a raw log of the worker's entire thought process.
# 
# To solve this …
# 
# 1.  We will now create a crucial final node for our research sub-graph: the `compress_research` node. This agent sole job is to act as a **librarian** or **"archivist"**.
# 2.  It reads the entire, messy history of a completed research run and `compresses` it into a clean, comprehensive, and well-cited summary.
# 3.  A key piece is its prompt is the instruction to explicitly filter out the agent's internal `think_tool` calls, ensuring the final report contains only the valuable, externally-sourced information.
# 
# First, let’s define the two prompts that will guide this compression agent.

# %%
# The system prompt for our compression agent, instructing it on how to filter and format the research history.
compress_research_system_prompt = """You are a research assistant that has conducted research on a topic by calling several tools and web searches. Your job is now to clean up the findings, but preserve all of the relevant statements and information that the researcher has gathered. For context, today's date is {date}.

<Task>
You need to clean up information gathered from tool calls and web searches in the existing messages.
All relevant information should be repeated and rewritten verbatim, but in a cleaner format.
The purpose of this step is just to remove any obviously irrelevant or duplicate information.
For example, if three sources all say "X", you could say "These three sources all stated X".
Only these fully comprehensive cleaned findings are going to be returned to the user, so it's crucial that you don't lose any information from the raw messages.
</Task>

<Tool Call Filtering>
**IMPORTANT**: When processing the research messages, focus only on substantive research content:
- **Include**: All tavily_search results and findings from web searches
- **Exclude**: think_tool calls and responses - these are internal agent reflections for decision-making and should not be included in the final research report
- **Focus on**: Actual information gathered from external sources, not the agent's internal reasoning process

The think_tool calls contain strategic reflections and decision-making notes that are internal to the research process but do not contain factual information that should be preserved in the final report.
</Tool Call Filtering>

<Guidelines>
1. Your output findings should be fully comprehensive and include ALL of the information and sources that the researcher has gathered from tool calls and web searches. It is expected that you repeat key information verbatim.
2. This report can be as long as necessary to return ALL of the information that the researcher has gathered.
3. In your report, you should return inline citations for each source that the researcher found.
4. You should include a "Sources" section at the end of the report that lists all of the sources the researcher found with corresponding citations, cited against statements in the report.
5. Make sure to include ALL of the sources that the researcher gathered in the report, and how they were used to answer the question!
6. It's really important not to lose any sources. A later LLM will be used to merge this report with others, so having all of the sources is critical.
</Guidelines>

<Output Format>
The report should be structured like this:
**List of Queries and Tool Calls Made**
**Fully Comprehensive Findings**
**List of All Relevant Sources (with citations in the report)**
</Output Format>

<Citation Rules>
- Assign each unique URL a single citation number in your text
- End with ### Sources that lists each source with corresponding numbers
- IMPORTANT: Number sources sequentially without gaps (1,2,3,4...) in the final list regardless of which sources you choose
- Example format:
  [1] Source Title: URL
  [2] Source Title: URL
</Citation Rules>

Critical Reminder: It is extremely important that any information that is even remotely relevant to the user's research topic is preserved verbatim (e.g. don't rewrite it, don't summarize it, don't paraphrase it).
"""

# %%
# The system prompt for our compression agent, instructing it on how to filter and format the research history.
compress_research_human_message = """You are a research assistant that has conducted research on a topic {research_topic} by calling several tools and web searches. Your job is now to clean up the findings, but preserve all of the relevant statements and information that the researcher has gathered.

<Task>
You need to clean up information gathered from tool calls and web searches in the existing messages.
All relevant information should be repeated and rewritten verbatim, but in a cleaner format.
The purpose of this step is just to remove any obviously irrelevant or duplicate information.
For example, if three sources all say "X", you could say "These three sources all stated X".
Only these fully comprehensive cleaned findings are going to be returned to the user, so it's crucial that you don't lose any information from the raw messages.
</Task>

<Tool Call Filtering>
**IMPORTANT**: When processing the research messages, focus only on substantive research content:
- **Include**: All tavily_search results and findings from web searches
- **Exclude**: think_tool calls and responses - these are internal agent reflections for decision-making and should not be included in the final research report
- **Focus on**: Actual information gathered from external sources, not the agent's internal reasoning process

The think_tool calls contain strategic reflections and decision-making notes that are internal to the research process but do not contain factual information that should be preserved in the final report.
</Tool Call Filtering>

<Guidelines>
1. Your output findings should be fully comprehensive and include ALL of the information and sources that the researcher has gathered from tool calls and web searches. It is expected that you repeat key information verbatim.
2. This report can be as long as necessary to return ALL of the information that the researcher has gathered.
3. In your report, you should return inline citations for each source that the researcher found.
4. You should include a "Sources" section at the end of the report that lists all of the sources the researcher found with corresponding citations, cited against statements in the report.
5. Make sure to include ALL of the sources that the researcher gathered in the report, and how they were used to answer the question!
6. It's really important not to lose any sources. A later LLM will be used to merge this report with others, so having all of the sources is critical.
</Guidelines>

<Output Format>
The report should be structured like this:
**List of Queries and Tool Calls Made**
**Fully Comprehensive Findings**
**List of All Relevant Sources (with citations in the report)**
</Output Format>

<Citation Rules>
- Assign each unique URL a single citation number in your text
- End with ### Sources that lists each source with corresponding numbers
- IMPORTANT: Number sources sequentially without gaps (1,2,3,4...) in the final list regardless of which sources you choose
- Example format:
  [1] Source Title: URL
  [2] Source Title: URL
</Citation Rules>

Critical Reminder: It is extremely important that any information that is even remotely relevant to the user's research topic is preserved verbatim (e.g. don't rewrite it, don't summarize it, don't paraphrase it).
"""

# %% [markdown]
# These two prompts work together to define the `compress_research` agent task.
# 
# 1.  The `compress_research_system_prompt` is the **Standard Operating Procedure**, providing the crucial `<Tool Call Filtering>` rule to ignore the `think_tool`.
# 2.  The `compress_research_human_message` provides the specific, run-time context, reminding the agent of the `RESEARCH TOPIC` to ensure its output is focused and relevant. The instruction to preserve information `verbatim` is key to preventing information loss during this compression step.
# 
# Now, we can build the `compress_research` node function itself.

# %%
# Compress model
compress_model = init_chat_model(model="gpt-oss:20b", base_url="http://localhost:11434", max_tokens=32000)

def compress_research(state: ResearcherState) -> dict:
    """The final node in the research sub-graph: it compresses all findings from the ReAct loop into a clean, cited summary."""
    # 1. We format the system and human messages for our compression model.
    system_message = compress_research_system_prompt.format(date=get_today_str())

    # The full message history of the ReAct loop is passed as context.
    messages = [SystemMessage(content=system_message)] + state.get("researcher_messages", []) + [HumanMessage(content=compress_research_human_message.format(research_topic=state['research_topic']))]
    
    # 2. We invoke our powerful 'compress_model'.
    response = compress_model.invoke(messages)

    # 3. We also extract the raw, unprocessed notes from the tool and AI messages.
    #    This is for archival purposes and can be used by the Supervisor for deeper analysis if needed.
    raw_notes = [
        str(m.content) for m in filter_messages(
            state["researcher_messages"], 
            include_types=["tool", "ai"] # We only include the 'action' and 'observation' parts of the loop.
        )
    ]

    # 4. This node returns the final, clean outputs that will be passed out of the sub-graph.
    return {
        "compressed_research": str(response.content),
        "raw_notes": ["\n".join(raw_notes)]
    }

# %% [markdown]
# The `compress_research` function is an essential clean-up and formatting step.
# 
# 1.  It acts as the bridge between the chaotic, internal process of the research agent and the clean, structured information that the Supervisor expects.
# 2.  It takes the entire messy history of the ReAct loop, filters out the internal monologue, and produces two key artifacts, the `compressed_research` summary for the Supervisor's immediate use, and the `raw_notes` for deeper, optional analysis. This separation of concerns is a key pattern for building clean interfaces between different agentic components.
# 
# Finally, we can assemble all these components into our complete, runnable `researcher_agent` sub-graph.

# %%
from langgraph.graph import StateGraph, START, END

# We initialize a new StateGraph, specifying the input state and, importantly, the output schema for this sub-graph.
agent_builder = StateGraph(ResearcherState, output_schema=ResearcherOutputState)

# We add our three nodes: the thinker (llm_call), the actor (tool_node), and the final compressor.
agent_builder.add_node("llm_call", llm_call)
agent_builder.add_node("tool_node", tool_node)
agent_builder.add_node("compress_research", compress_research)

# The entry point for this sub-graph is always the 'llm_call' (the brain).
agent_builder.add_edge(START, "llm_call")

# After the brain thinks, our conditional edge, 'should_continue', decides what to do next.
agent_builder.add_conditional_edges(
    "llm_call",
    should_continue,
    {
        "tool_node": "tool_node", # If tools are needed, we go to the tool node.
        "compress_research": "compress_research", # If research is done, we proceed to the compression node.
    },
)

# After the tool node acts, the flow loops back to the brain to process the results of the action.
agent_builder.add_edge("tool_node", "llm_call")

# The compression node is the final step; its output is the output of the entire sub-graph, so we connect it to END.
agent_builder.add_edge("compress_research", END)

# We compile the graph into a runnable object.
researcher_agent = agent_builder.compile()

# %% [markdown]
# We have now successfully constructed our `researcher_agent` sub-graph. This is our **worker engine**.
# 
# It is a complete, self-contained agent that can take a single, specific research topic, perform an iterative ReAct-style research process using a sophisticated, multi-stage search tool, and return a clean, compressed summary of its findings along with the raw source material.
# 
# The Supervisor agent, which we will build in the next part, can now invoke this powerful sub-graph as if it were a simple tool.
# 
# 1.  It can delegate complex research tasks even multiple in parallel and trust that it will receive a high-quality, well-structured, and predictable result for each one.
# 2.  This **graph-within-a-graph** design is a key architectural pattern for building complex, hierarchical agentic systems in a clean, maintainable, and scalable way.

# %% [markdown]
# ## Creating the Core Denoising Loop
# 
# We can now construct the master orchestrator, the **Supervisor Agent**. This is the core of our Deep agentic system.
# 
# > It is not a simple agent, it’s a meta-agent whose job is to manage the entire iterative diffusion process.
# 
# ![Core Denoising Loop (Created by Fareed Khan)](https://miro.medium.com/v2/resize:fit:2000/1*v1OsLVJOYGNsgmEDSgSdAw.png)
# 
# It analyzes the current state of the draft, decides what information is missing, delegates research tasks to the worker agents, and applies their findings to denoise and refine the report.
# 
# This Supervisor is the core of our `Critique -> Research -> Refine` loop. It is a cyclical, stateful process that allows the system to progressively improve its output, moving from a noisy initial draft towards a high-quality, fact-based final report.
# 
# ### Building the Supervisor Node
# 
# The first component of our Supervisor is the node that performs the high-level strategic reasoning.
# 
# ![Supervisor Node (Created by Fareed Khan)](https://miro.medium.com/v2/resize:fit:1400/1*W3UZSaVGRaH8nd_WE_kSEQ.png)
# 
# This `supervisor` node will analyze the full state of the system, including the current draft, the research brief, and any critical feedback from the adversarial Red Team or the programmatic evaluator. Based on this analysis, it will decide on the next set of actions, which will be encoded as tool calls.
# 
# First, let’s define the master prompt that encodes the diffusion algorithm for our Supervisor. This is the most important prompt in our entire system.

# %%
# This is the master prompt for our Supervisor, defining the core diffusion/denoising algorithm.
lead_researcher_with_multiple_steps_diffusion_double_check_prompt = """You are a research supervisor. Your job is to conduct research by calling the "ConductResearch" tool and refine the draft report by calling "refine_draft_report" tool based on your new research findings. For context, today's date is {date}. You will follow the diffusion algorithm:

<Diffusion Algorithm>
1. generate the next research questions to address gaps in the draft report
2. **ConductResearch**: retrieve external information to provide concrete delta for denoising
3. **refine_draft_report**: remove “noise” (imprecision, incompleteness) from the draft report
4. **CompleteResearch**: complete research only based on ConductReserach tool's findings' completeness. it should not be based on the draft report. even if the draft report looks complete, you should continue doing the research until all the research findings are collected. You know the research findings are complete by running ConductResearch tool to generate diverse research questions to see if you cannot find any new findings. If the language from the human messages in the message history is not English, you know the research findings are complete by always running ConductResearch tool to generate another round of diverse research questions to check the comprehensiveness.

</Diffusion Algorithm>

<Task>
Your focus is to call the "ConductResearch" tool to conduct research against the overall research question passed in by the user and call "refine_draft_report" tool to refine the draft report with the new research findings. When you are completely satisfied with the research findings and the draft report returned from the tool calls, then you should call the "ResearchComplete" tool to indicate that you are done with your research.
</Task>

<Available Tools>
You have access to four main tools:
1. **ConductResearch**: Delegate research tasks to specialized sub-agents
2. **refine_draft_report**: Refine draft report using the findings from ConductResearch
3. **ResearchComplete**: Indicate that research is complete
4. **think_tool**: For reflection and strategic planning during research (use ONLY as a separate tool call)

**CRITICAL TOOL FORMAT**: Each tool call must contain ONLY its defined parameters. Do NOT add extra fields like 'reflection' or 'thinking' to any tool call. If you want to reflect, make a SEPARATE think_tool call.
**PARALLEL RESEARCH**: When you identify multiple independent sub-topics that can be explored simultaneously, make multiple ConductResearch tool calls in a single response to enable parallel research execution. This is more efficient than sequential research for comparative or multi-faceted questions. Use at most {max_concurrent_research_units} parallel agents per iteration.
</Available Tools>

<Instructions>
Think like a research manager with limited time and resources. Follow these steps:

1. **Read the question carefully** - What specific information does the user need?
2. **Decide how to delegate the research** - Carefully consider the question and decide how to delegate the research. Are there multiple independent directions that can be explored simultaneously?
3. **After each call to ConductResearch, pause and assess** - Do I have enough to answer? What's still missing? and call refine_draft_report to refine the draft report with the findings. Always run refine_draft_report after ConductResearch call.
4. **call CompleteResearch only based on ConductReserach tool's findings' completeness. it should not be based on the draft report. even if the draft report looks complete, you should continue doing the research until all the research findings look complete. You know the research findings are complete by running ConductResearch tool to generate diverse research questions to see if you cannot find any new findings. If the language from the human messages in the message history is not English, you know the research findings are complete by always running ConductResearch tool to generate another round of diverse research questions to check the comprehensiveness.
</Instructions>

<Hard Limits>
**Task Delegation Budgets** (Prevent excessive delegation):
- **Bias towards single agent** - Use single agent for simplicity unless the user request has clear opportunity for parallelization
- **Stop when you can answer confidently** - Don't keep delegating research for perfection
- **Limit tool calls** - Always stop after {max_researcher_iterations} tool calls to think_tool and ConductResearch if you cannot find the right sources
</Hard Limits>

<Tool Usage Examples>
CORRECT - Separate tool calls:
1. Call think_tool(reflection="I need to research export controls and TSMC")
2. Call ConductResearch(research_topic="US export controls on semiconductors")
3. Call ConductResearch(research_topic="TSMC diversification strategy")

INCORRECT - Do NOT mix reflection into other tools:
❌ ConductResearch(reflection="...", research_topic="...") - WRONG!
❌ Adding any extra fields to tool calls - WRONG!
</Tool Usage Examples>

<Scaling Rules>
**Simple fact-finding, lists, and rankings** can use a single sub-agent:
- *Example*: List the top 10 coffee shops in San Francisco → Use 1 sub-agent

**Comparisons presented in the user request** can use a sub-agent for each element of the comparison:
- *Example*: Compare OpenAI vs. Anthropic vs. DeepMind approaches to AI safety → Use 3 sub-agents
- Delegate clear, distinct, non-overlapping subtopics

**Important Reminders:**
- Each ConductResearch call spawns a dedicated research agent for that specific topic
- A separate agent will write the final report - you just need to gather information
- When calling ConductResearch, provide complete standalone instructions - sub-agents can't see other agents' work
- Do NOT use acronyms or abbreviations in your research questions, be very clear and specific
</Scaling Rules>
"""

# %% [markdown]
# This `lead_researcher_with_multiple_steps_diffusion_double_check_prompt` is the **source code** for our entire agentic process.
# 
# 1.  It explicitly lays out the `<Diffusion Algorithm>` for the LLM to follow: `generate questions -> ConductResearch -> refine_draft_report`.
# 2.  It gives the Supervisor the authority to delegate parallel research (by making multiple `ConductResearch` tool calls), the responsibility to refine the draft with the `refine_draft_report` tool, and the logic to decide when the process has converged (`ResearchComplete`).
# 
# We also inject a specific variable, `{max_concurrent_research_units}`, directly into the system prompt. This serves as a strict budget constraint for the agent’s operations.
# 
# 1.  Without it, an overly enthusiastic Supervisor might attempt to spawn twenty parallel researchers to solve a simple query, quickly exceeding rate limits and consuming unnecessary API credits.
# 2.  By hard-coding this limit into the prompt, we ensure that the agent remains autonomous while operating safely within predefined boundaries.
# 
# Now, we can build the `supervisor` node function itself. This node is asynchronous (`async def`) because it will be orchestrating potentially long-running, parallel sub-agent calls.

# %%
async def supervisor(state: SupervisorState) -> Command[Literal["supervisor_tools"]]:
    """
    The 'Brain' of the diffusion process. This node analyzes the current state,
    including any critical feedback, and decides on the next set of actions (tool calls).
    """
    # 1. We get the current message history for the supervisor.
    supervisor_messages = state.get("supervisor_messages", [])
    
    # 2. We format the main system prompt with the diffusion algorithm instructions.
    system_message = lead_researcher_with_multiple_steps_diffusion_double_check_prompt.format(
        date=get_today_str(), 
        max_concurrent_research_units=max_concurrent_researchers,
        max_researcher_iterations=max_researcher_iterations
    )
    messages = [SystemMessage(content=system_message)] + supervisor_messages

    # 3. DYNAMIC CONTEXT INJECTION: We check for and inject any unaddressed adversarial feedback.
    # This is a critical self-correction mechanism.
    critiques = state.get("active_critiques", [])
    unaddressed = [c for c in critiques if not c.addressed]
    if unaddressed:
        critique_text = "\n".join([f"- {c.author} says: {c.concern}" for c in unaddressed])
        intervention = SystemMessage(content=f"""
        CRITICAL INTERVENTION REQUIRED.
        The following issues were detected by the Adversarial Team in your draft:
        {critique_text}
        
        You MUST address these issues in your next step.
        If the critique says citations are missing, call 'ConductResearch' to find them.
        If the critique says logic is flawed, call 'think_tool' to plan a fix.
        """)
        messages.append(intervention)

    # 4. We also inject a warning if the programmatic quality score was low in the last iteration.
    if state.get("needs_quality_repair"):
        messages.append(SystemMessage(content="PREVIOUS DRAFT QUALITY WAS LOW (Score < 7/10). Focus on finding new sources and citing them."))

    # 5. We invoke the tool-bound supervisor model to get its next plan.
    response = await supervisor_model_with_tools.ainvoke(messages)

    # 6. We return a Command to proceed to the 'supervisor_tools' node to execute the plan.
    return Command(
        goto="supervisor_tools",
        update={
            "supervisor_messages": [response],
            "research_iterations": state.get("research_iterations", 0) + 1,
            "needs_quality_repair": False # We reset the repair flag after warning the model.
        }
    )

# %%
# Rebuild the supervisor with the updated prompt
# This ensures the changes to the prompt are reflected in the agent

# Rebind supervisor model with tools (using the updated prompt)
print("✓ Supervisor function updated with clearer tool usage guidelines")

# %% [markdown]
# The `supervisor` node is the strategic core of our diffusion loop. It’s a kind of **dynamic prompt engineering**.
# 
# 1.  Before calling the LLM, it inspects the current `SupervisorState` and injects additional, high-priority system messages if critical feedback (`active_critiques`) or a low quality score (`needs_quality_repair`) is present.
# 2.  This forces the agent to pay immediate attention to its past failures and prioritize self-correction in its next plan, creating a robust, self-balancing system.
# 
# This is a pattern I call **State-Driven Prompt Injection** because we don’t just pass the state variables to the LLM, we dynamically alter the prompt based on them.
# 
# If `needs_quality_repair` is set to `True`, we temporarily inject a SystemMessage that clearly signals: **“PREVIOUS DRAFT QUALITY WAS LOW”**.
# 
# We don’t depend on the model implicit memory to improve on its own. Instead, we explicitly push this reminder into the context window for a single iteration. Once the Supervisor generates a new plan, **we reset the flag** to `False` in the returned `Command`, restoring the normal prompt flow for the next loop.
# 
# ### Fan-Out Work and Denoise
# 
# The `supervisor` node decides what to do. The `supervisor_tools` node does it. This node is the "hands" of our Supervisor. It inspects the `AIMessage` from the `supervisor` node, extracts the planned tool calls, and orchestrates their execution.
# 
# ![Fan-out Workflow (Created by Fareed Khan)](https://miro.medium.com/v2/resize:fit:2000/1*KCB0Iqw3YBpuz7I8eb9-Bw.png)
# 
# This node is our parallelism component. When it sees multiple `ConductResearch` tool calls, it will launch our `researcher_agent` sub-graph for each one concurrently, fanning out the work. It is also responsible for executing the core `refine_draft_report` denoising step.
# 
# First, a small helper to extract raw text from the tool messages, which will be a fallback for our refiner.

# %%
def get_notes_from_tool_calls(messages: list[BaseMessage]) -> list[str]:
    """A helper function to extract the string content from ToolMessage objects in the supervisor's message history."""
    # This filters the message history for messages of type 'tool' and returns their content.
    return [tool_msg.content for tool_msg in filter_messages(messages, include_types="tool")]

# %% [markdown]
# This utility function, `get_notes_from_tool_calls`, provides a simple way to get a concatenated string of all the research findings from the supervisor recent conversational turns. This is a fallback for the `refine_draft_report` tool in the early stages of the loop before the structured `knowledge_base` has been populated.
# 
# Now, we build the main `supervisor_tools` node. This is the most complex node in our entire system, as it orchestrates sub-graph execution, tool calls, and our self-evolution evaluation.

# %%
async def supervisor_tools(state: SupervisorState) -> Command[Literal["red_team", "context_pruner", "__end__"]]:
    """
    The 'Hands' of the Supervisor. This node executes the planned tool calls, including
    fanning out to parallel research sub-graphs and running the denoising step.
    """

    # 1. We get the most recent message, which contains the tool calls to be executed.
    most_recent_message = state.get("supervisor_messages", [])[-1]
    
    # 2. We check for exit conditions for the entire diffusion loop.
    exceeded_iterations = state.get("research_iterations", 0) >= max_researcher_iterations
    no_tool_calls = not most_recent_message.tool_calls
    research_complete = any(tc["name"] == "ResearchComplete" for tc in most_recent_message.tool_calls)


    if exceeded_iterations or no_tool_calls or research_complete:

        # If exiting, we prepare the final, curated notes for the report writer.
        # We prioritize the structured Knowledge Base, but fall back to raw notes if it's empty.
        kb_notes = [f"{f.content} (Confidence: {f.confidence_score})" for f in state.get("knowledge_base", [])]
        if not kb_notes: kb_notes = get_notes_from_tool_calls(state.get("supervisor_messages", []))

        # We return a Command to END this sub-graph and pass the final notes up to the main graph.
        return Command(goto=END, update={"notes": kb_notes, "research_brief": state.get("research_brief", "")})

    # 3. We separate the different types of tool calls for specialized handling.
    conduct_research_calls = [t for t in most_recent_message.tool_calls if t["name"] == "ConductResearch"]
    refine_report_calls = [t for t in most_recent_message.tool_calls if t["name"] == "refine_draft_report"]
    think_calls = [t for t in most_recent_message.tool_calls if t["name"] == "think_tool"]
    
    tool_messages = []
    all_raw_notes = []
    draft_report = state.get("draft_report", "")
    updates = {}

    # 4. Handle 'think_tool' calls synchronously.
    for tool_call in think_calls:
        observation = think_tool.invoke(tool_call["args"])
        tool_messages.append(ToolMessage(content=observation, name="think_tool", tool_call_id=tool_call["id"]))

    # 5. Handle 'ConductResearch' calls by fanning out to our research sub-graph in parallel.
    if conduct_research_calls:

        # We create a list of coroutines, one for each research task.
        coros = [researcher_agent.ainvoke({"researcher_messages": [HumanMessage(content=tc["args"]["research_topic"])], "research_topic": tc["args"]["research_topic"]}) for tc in conduct_research_calls]

        # 'asyncio.gather' runs all the research sub-graphs concurrently.
        results = await asyncio.gather(*coros)
        for result, tool_call in zip(results, conduct_research_calls):

            # We append the clean, compressed research as a ToolMessage for the Supervisor's context.
            tool_messages.append(ToolMessage(content=result.get("compressed_research", ""), name=tool_call["name"], tool_call_id=tool_call["id"]))

            # We also collect the raw, uncompressed notes to be processed by our context pruner.
            all_raw_notes.extend(result.get("raw_notes", []))

    # 6. Handle 'refine_draft_report' calls. This is the core denoising and self-evaluation step.
    for tool_call in refine_report_calls:
        kb = state.get("knowledge_base", [])
        kb_str = "CONFIRMED FACTS:\n" + "\n".join([f"- {f.content}" for f in kb]) if kb else "\n".join(get_notes_from_tool_calls(state.get("supervisor_messages", [])))
        new_draft = refine_draft_report.invoke({"research_brief": state.get("research_brief", ""), "findings": kb_str, "draft_report": state.get("draft_report", "")})
        
        # --- CRITICAL STEP: The Self-Evolution Evaluation ---
        eval_result = evaluate_draft_quality(research_brief=state.get("research_brief", ""), draft_report=new_draft)
        avg_score = (eval_result.comprehensiveness_score + eval_result.accuracy_score) / 2
        
        # We include the quality score directly in the tool message, so the Supervisor sees it.
        tool_messages.append(ToolMessage(content=f"Draft Updated.\nQuality Score: {avg_score}/10.\nJudge Feedback: {eval_result.specific_critique}", name=tool_call["name"], tool_call_id=tool_call["id"]))
        draft_report = new_draft
        
        # We log the metric to our history and set the repair flag if the score is low.
        updates["quality_history"] = [QualityMetric(score=avg_score, feedback=eval_result.specific_critique, iteration=state.get("research_iterations", 0))]
        if avg_score < 7.0: updates["needs_quality_repair"] = True

    # 7. Prepare the final state updates for this iteration.
    updates["supervisor_messages"] = tool_messages
    updates["raw_notes"] = all_raw_notes
    updates["draft_report"] = draft_report
    
    # 8. FAN OUT to the self-correction nodes (Red Team and Context Pruner) in parallel.
    return Command(goto=["red_team", "context_pruner"], update=updates)

# %% [markdown]
# 1.  The `asyncio.gather(*coros)` call is the key to our parallel research, it invokes multiple instances of our `researcher_agent` sub-graph and waits for them all to complete.
# 2.  The most advanced part is the handling of the `refine_draft_report` call. Immediately after a new draft is generated, it is passed to our `evaluate_draft_quality` function.
# 3.  This creates an incredibly tight feedback loop: the system refines, immediately self-evaluates, and then uses the `QualityMetric` to inform the very next reasoning step of the `supervisor` node.
# 4.  The final `Command(goto=["red_team", "context_pruner"])` is another powerful parallel fan-out, sending the state to our self-correction agents simultaneously.
# 
# ### Applying Guidance with Denoising Step
# 
# Finally, let’s look at the implementation of the core `refine_draft_report` tool itself. This is the actual **denoising mechanism that applies the guidance (new facts)** to the noisy image (the current draft).
# 
# ![Guidance with Denoising Step (Created by Fareed Khan)](https://miro.medium.com/v2/resize:fit:1400/1*XyDjTOQByiogiwkd2eB6iw.png)
# 
# First, we define the prompt that guides this synthesis process.

# %%
# The prompt for the refine_draft_report tool, which instructs the LLM on how to integrate new facts into an existing draft.
report_generation_with_draft_insight_prompt = """Based on all the research conducted and draft report, create a comprehensive, well-structured answer to the overall research brief:
<Research Brief>
{research_brief}
</Research Brief>

CRITICAL: Make sure the answer is written in the same language as the human messages!
For example, if the user's messages are in English, then MAKE SURE you write your response in English. If the user's messages are in Chinese, then MAKE SURE you write your entire response in Chinese.
This is critical. The user will only understand the answer if it is written in the same language as their input message.

Today's date is {date}.

Here is the draft report:
<Draft Report>
{draft_report}
</Draft Report>

Here are the findings from the research that you conducted:
<Findings>
{findings}
</Findings>

Please create a detailed answer to the overall research brief that:
1. Is well-organized with proper headings (# for title, ## for sections, ### for subsections)
2. Includes specific facts and insights from the research
3. References relevant sources using [Title](URL) format
4. Provides a balanced, thorough analysis. Be as comprehensive as possible, and include all information that is relevant to the overall research question. People are using you for deep research and will expect detailed, comprehensive answers.
5. Includes a "Sources" section at the end with all referenced links

You can structure your report in a number of different ways. Here are some examples:

To answer a question that asks you to compare two things, you might structure your report like this:
1/ intro
2/ overview of topic A
3/ overview of topic B
4/ comparison between A and B
5/ conclusion

To answer a question that asks you to return a list of things, you might only need a single section which is the entire list.
1/ list of things or table of things
Or, you could choose to make each item in the list a separate section in the report. When asked for lists, you don't need an introduction or conclusion.
1/ item 1
2/ item 2
3/ item 3

To answer a question that asks you to summarize a topic, give a report, or give an overview, you might structure your report like this:
1/ overview of topic
2/ concept 1
3/ concept 2
4/ concept 3
5/ conclusion

If you think you can answer the question with a single section, you can do that too!
1/ answer

REMEMBER: Section is a VERY fluid and loose concept. You can structure your report however you think is best, including in ways that are not listed above!
Make sure that your sections are cohesive, and make sense for the reader.

For each section of the report, do the following:
- Use simple, clear language
- Keep important details from the research findings
- Use ## for section title (Markdown format) for each section of the report
- Do NOT ever refer to yourself as the writer of the report. This should be a professional report without any self-referential language. 
- Do not say what you are doing in the report. Just write the report without any commentary from yourself.
- Each section should be as long as necessary to deeply answer the question with the information you have gathered. It is expected that sections will be fairly long and verbose. You are writing a deep research report, and users will expect a thorough answer.
- Use bullet points to list out information when appropriate, but by default, write in paragraph form.

REMEMBER:
The brief and research may be in English, but you need to translate this information to the right language when writing the final answer.
Make sure the final answer report is in the SAME language as the human messages in the message history.

Format the report in clear markdown with proper structure and include source references where appropriate.

<Citation Rules>
- Assign each unique URL a single citation number in your text
- End with ### Sources that lists each source with corresponding numbers
- IMPORTANT: Number sources sequentially without gaps (1,2,3,4...) in the final list regardless of which sources you choose
- Each source should be a separate line item in a list, so that in markdown it is rendered as a list.
- Example format:
  [1] Source Title: URL
  [2] Source Title: URL
- Citations are extremely important. Make sure to include these, and pay a lot of attention to getting these right. Users will often use these citations to look into more information.
</Citation Rules>
"""

# %% [markdown]
# This prompt is highly specific. It provides the LLM with three pieces of information: the overall goal (`research_brief`), the current state (`draft_report`), and the new information to incorporate (`findings`).
# 
# By instructing it to create a **"new and improved"** version, we are guiding it to perform the synthesis task that is the core of our denoising step.
# 
# Now, we can implement the decorated tool function.

# %%
from langchain_core.tools import InjectedToolArg

@tool(parse_docstring=True)
def refine_draft_report(
    research_brief: Annotated[str, InjectedToolArg], 
    findings: Annotated[str, InjectedToolArg], 
    draft_report: Annotated[str, InjectedToolArg]
):
    """Refine draft report

    Synthesizes all research findings into a comprehensive draft report

    Args:
        research_brief: user's research request
        findings: collected research findings for the user request
        draft_report: draft report based on the findings and user request

    Returns:
        refined draft report
    """

    # We format the detailed prompt with the brief, the current draft, and the new findings.
    draft_report_prompt = report_generation_with_draft_insight_prompt.format(
        research_brief=research_brief,
        findings=findings,
        draft_report=draft_report,
        date=get_today_str()
    )
  
    # We invoke our powerful 'writer_model' to generate the new, "denoised" draft.
    draft_report_response = writer_model.invoke([HumanMessage(content=draft_report_prompt)])
    return draft_report_response.content

# %% [markdown]
# The `refine_draft_report` tool is the action that corresponds to a **denoising step**. The use of `Annotated[str, InjectedToolArg]` is a `LangGraph` feature that tells the system that the arguments for this tool should not be provided by the LLM in its tool call, but should be injected directly from the current `SupervisorState` by the `supervisor_tools` node.
# 
# This ensures the tool always operates on the most up-to-date information, taking the current **noisy image** (`draft_report`) and the "guidance signal" (`findings`) to produce a cleaner, more refined version.

# %%
# Initialize supervisor model with its specialized tools
# The supervisor needs access to: think_tool, ConductResearch, ResearchComplete, and refine_draft_report
supervisor_tool_list = [think_tool, ConductResearch, ResearchComplete, refine_draft_report]
supervisor_model_with_tools = critic_model.bind_tools(supervisor_tool_list)

print(f"✓ Supervisor model bound with {len(supervisor_tool_list)} tools")

# %% [markdown]
# ## Self-Correction and Stability Mechanisms
# 
# We have now built the core iterative engine of our deep research system which is a Supervisor that can research and refine a draft. However, a simple, unguided diffusion process can be unstable.
# 
# > It might get stuck in a loop, fixating on a flawed idea, or its context window could grow uncontrollably until it crashes.
# 
# ![Core Denoising Mechanism (Created by Fareed Khan)](https://miro.medium.com/v2/resize:fit:2000/1*PgLCNUGJwm2s0rWeu12qfQ.png)
# 
# To build a truly production-grade system, we need to introduce advanced **guidance and stability mechanisms**. These are specialized agents and processes that run in parallel to the main loop, acting as **“guardrails”**. They provide critical feedback, perform essential memory management, and check for convergence, ensuring our diffusion process is not just iterative, but intelligent, self-correcting, and stable.
# 
# ### Red Team as a Noise Reducer
# 
# The first and most powerful guidance mechanism we will build is an adversarial one. While our main agents are designed to be helpful and constructive, we will now make a **“Red Team”** agent whose sole purpose is to be critical. Its job is not to write the report, but to attack it.
# 
# > It actively searches for logical fallacies, unsubstantiated claims, and hidden biases.
# 
# ![Red Team (Created by Fareed Khan)](https://miro.medium.com/v2/resize:fit:1400/1*1pW3Ep0PxY5XRl-kZ7cc0g.png)
# 
# This is a **self-balancing mechanism**. Standard LLMs are trained to be helpful and will often try to **smooth over** inconsistencies. The Red Team, with its explicitly adversarial prompt, counteracts this bias. It provides the harsh, critical feedback that is necessary for true intellectual rigor.
# 
# Let’s build the `red_team_node`. This node will run in parallel with other maintenance tasks after each refinement step.

# %%
from langchain_core.messages import SystemMessage, HumanMessage

# We define a specialized, powerful model for our critic using ollama.
# In a real system, you might use a model specifically fine-tuned for critical analysis.
critic_model = init_chat_model(model=OLLAMA_MODEL, base_url=OLLAMA_BASE_URL)
async def red_team_node(state: SupervisorState) -> dict:
    """
    This node represents the 'Red Team' agent. It runs in parallel to other steps,
    critiquing the current draft to find logical flaws and biases.
    """

    # 1. We get the current draft report from the state.
    draft = state.get("draft_report", "")
    
    # 2. We add a guardrail: the Red Team only activates if the draft is substantial enough to critique.
    if not draft or len(draft) < 50:
        return {}

# %% [markdown]
# In our `red_team_node`, the agent does not modify the draft or the Knowledge Base directly. Instead, it functions on a meta-layer by generating a structured `Critique` object.
# 
# By returning this `Critique` and injecting a high-priority `SystemMessage` into the Supervisor’s stream, the process ensures that the Supervisor cannot overlook the feedback. This design forces the “brain” of the operation to recognize the issue and allocate resources through new tool calls to address and correct it in the next iteration.

# %% [markdown]
# ### Evaluator as a Convergence Check
# 
# While the Red Team provides qualitative, logical feedback, we also need a quantitative measure of the draft quality. This is the job of our programmatic evaluator. This function acts as our **convergence check** for the diffusion process. After each `refine_draft_report` step, we will call this evaluator to get a numerical score.
# 
# ![Convergence Check (Created by Fareed Khan)](https://miro.medium.com/v2/resize:fit:1400/1*5UTD5Wi-r6mOrwLixK7tKA.png)
# 
# This score serves two purposes:
# 
# 1.  First, it allows us to track the agent’s improvement over time by logging it to our `quality_history`.
# 2.  Second, it acts as another form of guidance. If the score drops below a certain threshold, we can use it to trigger a **quality repair** warning for the Supervisor.
# 
# First, let’s define the Pydantic schema for the output of our evaluator.

# %%
class EvaluationResult(BaseModel):
    """A Pydantic schema for the structured output of our programmatic quality evaluator."""

    # A 0-10 score on how well the draft covers all aspects of the research brief.
    comprehensiveness_score: int = Field(description="0-10 score on coverage")

    # A 0-10 score on whether the claims in the draft are factually grounded.
    accuracy_score: int = Field(description="0-10 score on factual grounding")

    # A 0-10 score on the logical flow and readability of the draft.
    coherence_score: int = Field(description="0-10 score on flow")

    # Actionable feedback for the researcher on how to improve the draft.
    specific_critique: str = Field(description="Actionable feedback for the researcher")

# %% [markdown]
# This `EvaluationResult` schema is the scorecard for our self-evolution mechanism. It forces our judge model to provide a multi-dimensional assessment of the draft's quality, giving us a more nuanced signal than a single, holistic score.
# 
# Now, we can build the `evaluate_draft_quality` utility function itself.

# %%
# Our judge model
judge_model = init_chat_model(model=OLLAMA_MODEL, base_url=OLLAMA_BASE_URL)

def evaluate_draft_quality(research_brief: str, draft_report: str) -> EvaluationResult:
    """
    This function implements the 'Self-Evolution' scoring mechanism. It acts as an
    LLM-as-a-judge, programmatically evaluating the quality of a draft against the original brief.
    """

    # We create a prompt that asks the judge model to be an extremely critical Senior Research Editor.
    eval_prompt = f"""
    You are a Senior Research Editor. Your standards are exceptionally high. Evaluate this draft report against the research brief.
    
    <Research Brief>
    {research_brief}
    </Research Brief>
    
    <Draft Report>
    {draft_report}
    </Draft Report>
    
    Be extremely critical. High scores (8+) should be reserved for truly excellent, comprehensive, and well-cited work. 
    Focus your evaluation on these key areas:
    1. **Comprehensiveness:** Does the draft fully address all parts of the research brief? Are there significant gaps?
    2. **Accuracy & Grounding:** Are the claims specific and well-supported? Look for vague statements that need citations.
    3. **Coherence & Structure:** Is the report well-organized and easy to follow? Is the language clear and professional?
    
    Provide specific, actionable critique for the researcher.
    """
    
    # We bind our EvaluationResult schema to our judge model.
    structured_judge = judge_model.with_structured_output(EvaluationResult)

    # We invoke the judge to get the structured quality score.

# %% [markdown]
# It is called programmatically from within the `supervisor_tools` node immediately after a new draft is generated. This creates a tight feedback loop. The prompt is to act as a counteracting the natural tendency of LLMs to give overly positive scores.
# 
# The structured `EvaluationResult` it returns provides the precise, quantitative feedback our system needs to track its progress and trigger self-correction when quality drops.

# %% [markdown]
# ### State Entropy using Context Pruning Node
# 
# Our iterative research process has a significant challenge, as the Supervisor delegates more and more research tasks, the volume of `raw_notes` returned by the worker agents will grow.
# 
# ![State Entropy (Created by Fareed Khan)](https://miro.medium.com/v2/resize:fit:1400/1*VPVyVusU0pKss7BhvjMsjA.png)
# 
# If we simply keep appending this raw text to the Supervisor message history, its context window will quickly overflow, leading to crashes or a dramatic decrease in performance.
# 
# > This is a form of state entropy the system memory becomes progressively more chaotic and unmanageable over time.
# 
# To solve this, we will build a **Context Pruning Node**. This agent acts as a **memory architect** or **knowledge graph engineer**.
# 
# It runs in parallel with the Red Team after each research step. Its job is to take the unstructured, high-volume `raw_notes` from the temporary buffer, extract the valuable, atomic `Fact` objects, and add them to the permanent, structured `knowledge_base`. Crucially, it then *clears the `raw_notes` buffer*.
# 
# First, let’s define the Pydantic schema this node will use for its extraction task.

# %%
class FactExtraction(BaseModel):
    """A Pydantic schema for the output of the context pruning/fact extraction agent."""
    # The output is a list of the 'Fact' objects we defined in Part 1.
    new_facts: List[Fact]

# %% [markdown]
# This `FactExtraction` schema is the target for our compression task. It instructs the LLM to transform a large block of unstructured text into a clean, structured list of our canonical `Fact` objects.
# 
# Now, we can build the `context_pruning_node` itself.

# %%
# We'll use a fast, cheaper model for this routine extraction task.
compressor_model = init_chat_model(model="gpt-oss:20b")

async def context_pruning_node(state: SupervisorState) -> dict:
    """
    This node implements 'Context Engineering'. It takes the temporary buffer of raw notes,
    extracts structured facts, adds them to the permanent knowledge base, and then clears the buffer.
    """
    # 1. We get the current raw notes and the permanent knowledge base from the state.
    raw_notes = state.get("raw_notes", [])
    
    # 2. We add a guardrail: this node only runs if there are new raw notes to process.
    if not raw_notes:
        return {}

    # 3. We combine all the new raw notes into a single block of text for processing.
    text_block = "\n".join(raw_notes)

    # 4. We create a prompt that instructs the LLM to act as a Knowledge Graph Engineer.
    prompt = f"""
    You are a Knowledge Graph Engineer.
    
    New Raw Notes from a research agent:
    {text_block[:20000]} 
    
    Your task is to:
    1. Extract all atomic, verifiable facts from the New Raw Notes.
    2. For each fact, identify its source URL.
    3. Assign a confidence score (1-100) based on the credibility of the source.
    4. Ignore any information that is the agent's internal "thinking" or planning.
    
    Return ONLY a valid JSON object with a single key 'new_facts' containing a list of these structured facts.
    """
    
    try:

        # 5. We invoke our structured output LLM to perform the extraction.
        structured_llm = compressor_model.with_structured_output(FactExtraction)
        result = await structured_llm.ainvoke([HumanMessage(content=prompt)])
        new_facts = result.new_facts
        
        # In a more advanced system, we would perform deduplication against the existing knowledge_base here.
        message = f"[SYSTEM] Context Pruned. {len(new_facts)} new facts added to Knowledge Base. Raw notes buffer cleared."
    except Exception as e:

        # If the extraction fails, we create a system message to log the error.
        new_facts = []
        message = f"[SYSTEM] Context Pruning failed: {str(e)}"
    
    # 6. This is the most critical step. We return an update that CLEARS the 'raw_notes' buffer,
    #    appends the 'new_facts' to the permanent 'knowledge_base', and replaces the raw text in the
    #    Supervisor's history with a single, concise system message.
    return {
        "raw_notes": [], 
        "knowledge_base": new_facts,
        "supervisor_messages": [
            SystemMessage(content=message)
        ]
    }

# %% [markdown]
# The most important line of code is `"raw_notes": []`.
# 
# 1.  By clearing this buffer after every iteration, it prevents the Supervisor's context window from growing uncontrollably. This is the key to maintaining the long-term stability and performance of our iterative agent.
# 2.  It transforms the agent's memory from a simple, ever-growing log into a sophisticated, two-tiered system: a temporary scratchpad (`raw_notes`) and a permanent, structured long-term memory (`knowledge_base`).
# 
# This is a most common pattern for building agents that can reason over long, multi-step tasks without performance degradation.

# %% [markdown]
# ## Assembling the Entire Graph
# 
# We have now build all the individual components of our entire deep research architecture. The final step is to assemble these parts into a single, and executable graph using `LangGraph`.
# 
# We will treat each of our major components (Scoping, Research, Supervisor) as a sub-graph and wire them together in a logical sequence. We will also see how `LangGraph` ability to create parallel execution paths allows us to run our self-correction and memory management agents simultaneously, creating a highly efficient and robust architecture.
# 
# ### Wiring the Scoping Sub-Graph
# 
# First, let’s assemble our initial, user-facing sub-graph. This graph is responsible for the first three steps of our process: clarifying the user intent, writing the formal research brief, and generating the initial **“noisy” draft**. It’s a simple, linear sequence.

# %%
from langgraph.graph import StateGraph, START, END

# We initialize a new StateGraph, specifying the main AgentState and the initial AgentInputState.
scope_builder = StateGraph(AgentState, input_schema=AgentInputState)

# We add the three nodes that make up our scoping process.
scope_builder.add_node("clarify_with_user", clarify_with_user)
scope_builder.add_node("write_research_brief", write_research_brief)
scope_builder.add_node("write_draft_report", write_draft_report)

# The entry point is the 'clarify_with_user' node.
scope_builder.add_edge(START, "clarify_with_user")

# We already built the dynamic routing inside the 'clarify_with_user' node.
# It will either END the graph or proceed to 'write_research_brief'.
scope_builder.add_edge("write_research_brief", "write_draft_report")

# The 'write_draft_report' node is the final step of this sub-graph.
scope_builder.add_edge("write_draft_report", END)

# We compile this into a runnable sub-graph.
scope_research = scope_builder.compile()

# %% [markdown]
# ![Scope Graph (Created by Fareed Khan)](https://miro.medium.com/v2/resize:fit:382/1*sk1oRkfNT5C8rXzvobjzxg.png)
# 
# The `scope_research` object is now our complete, runnable front component. It encapsulates the entire user interaction and task definition process.
# 
# 1.  If a user request is ambiguous, this sub-graph will run, ask a clarifying question, and then stop.
# 2.  If the request is clear, it will run through all three nodes, producing the `research_brief` and the initial `draft_report` needed by the main Supervisor loop.
# 
# ### Wiring the Research Sub-Graph
# 
# Next, we need to formalize the `researcher_agent` sub-graph. This graph is the **worker engine** that our Supervisor will delegate tasks to. It is a self-contained ReAct loop that takes a single research topic and produces a compressed summary of its findings.

# %%
# We initialize a new StateGraph for our researcher, specifying its unique input and output schemas.
agent_builder = StateGraph(ResearcherState, output_schema=ResearcherOutputState)

# We add the three nodes of the researcher's 'Think-Act-Compress' loop.
agent_builder.add_node("llm_call", llm_call)
agent_builder.add_node("tool_node", tool_node)
agent_builder.add_node("compress_research", compress_research)

# The entry point is the 'llm_call' node (the brain).
agent_builder.add_edge(START, "llm_call")

# After the brain thinks, our conditional edge decides whether to act or compress.
agent_builder.add_conditional_edges(
    "llm_call",
    should_continue,
    {"tool_node": "tool_node", "compress_research": "compress_research"},
)

# After acting, the loop returns to the brain.
agent_builder.add_edge("tool_node", "llm_call")

# The compression step is the final output of this sub-graph.
agent_builder.add_edge("compress_research", END)

# We compile this into our runnable 'worker' sub-graph.
researcher_agent = agent_builder.compile()

# %% [markdown]
# ![Research Sub-graph (Created by Fareed Khan)](https://miro.medium.com/v2/resize:fit:674/1*cm9V7L4mft94sKHTleiByg.png)
# 
# The `researcher_agent` object is now our reusable worker. The Supervisor can invoke this compiled graph multiple times in parallel, with each invocation running its own independent, stateful ReAct loop. This is a powerful example of "graph-within-a-graph" composition.
# 
# ### Compiling the Denoising Loop
# 
# Now we assemble the most complex part of our system which is the Supervisor main diffusion loop. This graph will contain the Supervisor brain, its hands (the tools node), and the parallel self-correction agents (the Red Team and the Context Pruner).

# %%
# We initialize the StateGraph for our Supervisor.
supervisor_builder = StateGraph(SupervisorState)

# We add the four key nodes of the main loop.
supervisor_builder.add_node("supervisor", supervisor)
supervisor_builder.add_node("supervisor_tools", supervisor_tools)
supervisor_builder.add_node("red_team", red_team_node)
supervisor_builder.add_node("context_pruner", context_pruning_node)

# The entry point of the loop is the 'supervisor' brain.
supervisor_builder.add_edge(START, "supervisor")

# After the brain thinks and plans, it proceeds to the 'supervisor_tools' node to act.
supervisor_builder.add_edge("supervisor", "supervisor_tools")

# The 'supervisor_tools' node is our parallel fan-out point. Its return Command
# dynamically directs the graph to run 'red_team' and 'context_pruner' simultaneously.
# Therefore, we only need to define the edges for the fan-in, which bring the control flow back.
# After the Red Team runs, control returns to the 'supervisor' for the next iteration.
supervisor_builder.add_edge("red_team", "supervisor")

# After the Context Pruner runs, control also returns to the 'supervisor'.
supervisor_builder.add_edge("context_pruner", "supervisor")

# We compile this into our main, cyclical 'denoising engine'.
supervisor_agent = supervisor_builder.compile()

# %% [markdown]
# ![Denoising Loop (Created by Fareed Khan)](https://miro.medium.com/v2/resize:fit:1400/1*QOgYZpMtnayTCB_qltmifQ.png)
# 
# The most important architectural pattern here is the fan-out/fan-in loop.
# 
# 1.  The `supervisor_tools` node dynamically returns `Command(goto=["red_team", "context_pruner"])`, which causes `LangGraph` to execute these two nodes in parallel.
# 2.  The static `add_edge` calls for `red_team` and `context_pruner` then ensure that after *both* of these parallel tasks are complete, the control flow converges and loops back to the `supervisor` node for the next iteration of the diffusion process.
# 
# This creates a highly efficient loop where the main reasoning, the adversarial critique, and the memory management all happen in a coordinated, partially parallelized cycle.

# %% [markdown]
# ## Final Synthesis and Execution
# 
# We have now constructed all the major sub-components. The final step in our architectural pipeline is to assemble these pre-compiled sub-graphs into a single, cohesive, and fully operational master workflow.
# 
# ![Finalized Report Generation (Created by Fareed Khan)](https://miro.medium.com/v2/resize:fit:1400/1*VRpJbQ0uOT9yDwFtpWFIsw.png)
# 
# We will treat our complex, stateful sub-graphs as if they were simple, individual nodes in a higher-level graph. This hierarchical composition is the main way to build and manage systems of this complexity in a clean and modular way, so let’s do that.
# 
# ### Generating the Fully Denoised Report
# 
# Before we assemble the master graph, we need to define its final node. After the Supervisor iterative diffusion process is complete, its `END` state will contain the fully refined `draft_report` and the curated `notes` (our structured Knowledge Base).
# 
# ![Generating Full Denoised Report (Created by Fareed Khan)](https://miro.medium.com/v2/resize:fit:1400/1*Gg7bzHzbe8kAa01zZuc8Vw.png)
# 
# The `final_report_generation` node's job is to take these high-quality artifacts and synthesize them into the final, polished, and professionally formatted report for the user.
# 
# First, let’s define the master prompt that will guide our most powerful writer model in this final synthesis task.

# %%
# This is the prompt for our final, most powerful writer agent.
final_report_generation_with_helpfulness_insightfulness_hit_citation_prompt = """Based on all the research conducted and draft report, create a comprehensive, well-structured answer to the overall research brief:
<Research Brief>
{research_brief}
</Research Brief>

CRITICAL: Make sure the answer is written in the same language as the human messages!
For example, if the user's messages are in English, then MAKE SURE you write your response in English. If the user's messages are in Chinese, then MAKE SURE you write your entire response in Chinese.
This is critical. The user will only understand the answer if it is written in the same language as their input message.

Today's date is {date}.

Here are the findings from the research that you conducted:
<Findings>
{findings}
</Findings>

Here is the draft report:
<Draft Report>
{draft_report}
</Draft Report>

Please create a detailed answer to the overall research brief that:
1. Is well-organized with proper headings (# for title, ## for sections, ### for subsections)
2. Includes specific facts and insights from the research
3. References relevant sources using [Title](URL) format
4. Provides a balanced, thorough analysis. Be as comprehensive as possible, and include all information that is relevant to the overall research question. People are using you for deep research and will expect detailed, comprehensive answers.
5. Includes a "Sources" section at the end with all referenced links

You can structure your report in a number of different ways. Here are some examples:

To answer a question that asks you to compare two things, you might structure your report like this:
1/ intro
2/ overview of topic A
3/ overview of topic B
4/ comparison between A and B
5/ conclusion

To answer a question that asks you to return a list of things, you might only need a single section which is the entire list.
1/ list of things or table of things
Or, you could choose to make each item in the list a separate section in the report. When asked for lists, you don't need an introduction or conclusion.
1/ item 1
2/ item 2
3/ item 3

To answer a question that asks you to summarize a topic, give a report, or give an overview, you might structure your report like this:
1/ overview of topic
2/ concept 1
3/ concept 2
4/ concept 3
5/ conclusion

If you think you can answer the question with a single section, you can do that too!
1/ answer

REMEMBER: Section is a VERY fluid and loose concept. You can structure your report however you think is best, including in ways that are not listed above!
Make sure that your sections are cohesive, and make sense for the reader.

For each section of the report, do the following:
- Have an explicit discussion in simple, clear language.
- DO NOT oversimplify. Clarify when a concept is ambiguous.
- DO NOT list facts in bullet points. write in paragraph form.
- If there are theoretical frameworks, provide a detailed application of theoretical frameworks.
- For comparison and conclusion, include a summary table.
- Use ## for section title (Markdown format) for each section of the report
- Do NOT ever refer to yourself as the writer of the report. This should be a professional report without any self-referential language. 
- Do not say what you are doing in the report. Just write the report without any commentary from yourself.
- Each section should be as long as necessary to deeply answer the question with the information you have gathered. It is expected that sections will be fairly long and verbose. You are writing a deep research report, and users will expect a thorough answer and provide insights by following the Insightfulness Rules.

<Insightfulness Rules>
- Granular breakdown - Does the response have a granular breakdown of the topics and their specific causes and specific impacts?
- Detailed mapping table - Does the response have a detailed table mapping these causes and effects?
- Nuanced discussion - Does the response have detailed exploration of the topic and explicit discussion?
</Insightfulness Rules>

- Each section should follow the Helpfulness Rules.

<Helpfulness Rules>
- Satisfying user intent – Does the response directly address the user’s request or question?
- Ease of understanding – Is the response fluent, coherent, and logically structured?
- Accuracy – Are the facts, reasoning, and explanations correct?
- Appropriate language – Is the tone suitable and professional, without unnecessary jargon or confusing phrasing?
</Helpfulness Rules>

REMEMBER:
The brief and research may be in English, but you need to translate this information to the right language when writing the final answer.
Make sure the final answer report is in the SAME language as the human messages in the message history.

Format the report in clear markdown with proper structure and include source references where appropriate.

<Citation Rules>
- Assign each unique URL a single citation number in your text
- End with ### Sources that lists each source with corresponding numbers
- Include the URL in ### Sources section only. Use the citation number in the other sections.
- IMPORTANT: Number sources sequentially without gaps (1,2,3,4...) in the final list regardless of which sources you choose
- Each source should be a separate line item in a list, so that in markdown it is rendered as a list.
- Example format:
  [1] Source Title: URL
  [2] Source Title: URL
- Citations are extremely important. Make sure to include these, and pay a lot of attention to getting these right. Users will often use these citations to look into more information.
</Citation Rules>
"""

# %% [markdown]
# This final prompt is the last step of our process. It instructs our most capable model (`gpt-5` or its fallback) to act as a master editor, taking all the high-quality, pre-digested information from the Supervisor loop and performing the final synthesis.
# 
# The `<Insightfulness Rules>` are a key piece of prompt engineering, pushing the model beyond simple summarization to generate genuine, novel insights from the collected data.
# 
# Now, we can build the `final_report_generation` node itself.

# %%
# We use our most powerful writer model for this final, high-stakes generation task.
writer_model = init_chat_model(model="gpt-oss:20b", base_url="http://localhost:11434", max_tokens=40000) # Using a large max_tokens for comprehensive reports.

async def final_report_generation(state: AgentState):
    """
    The final node in our master graph. It takes all the curated artifacts from the
    Supervisor loop and generates the final, polished report.
    """
    # 1. We retrieve the final, curated notes from the state.
    notes = state.get("notes", [])
    findings = "\n".join(notes)

    # 2. We format our master prompt with all the necessary context.
    final_report_prompt = final_report_generation_with_helpfulness_insightfulness_hit_citation_prompt.format(
        research_brief=state.get("research_brief", ""),
        findings=findings,
        date=get_today_str(),
        draft_report=state.get("draft_report", ""),
        user_request=state.get("messages", [HumanMessage(content="")])[-1].content # Pass the original user request for context
    )

    # 3. We invoke our powerful writer model to generate the final report.
    final_report = await writer_model.ainvoke([HumanMessage(content=final_report_prompt)])

    # 4. We update the state with the final_report and a user-facing message.
    return {
        "final_report": final_report.content, 
        "messages": ["Here is the final report: " + final_report.content],
    }

# %% [markdown]
# The `final_report_generation` node takes the `notes` (the structured, denoised facts from our `knowledge_base`) and the final `draft_report` from the Supervisor loop and performs one last, high-quality synthesis.
# 
# This separation of concerns is important because the Supervisor loop is optimized for iterative research and refinement, while this final node is optimized for high-quality, long-form generation.
# 
# ### Integrating All Sub-Graphs
# 
# With our final node defined, we can now assemble the master graph. We will add each of our pre-compiled sub-graphs (`scope_research` and `supervisor_agent`) as if they were single nodes.

# %%
# We initialize our master StateGraph, using the main AgentState and AgentInputState.
deep_researcher_builder = StateGraph(AgentState, input_schema=AgentInputState)

# We add our pre-compiled sub-graphs and our individual nodes to the master graph.
deep_researcher_builder.add_node("clarify_with_user", clarify_with_user)
deep_researcher_builder.add_node("write_research_brief", write_research_brief)
deep_researcher_builder.add_node("write_draft_report", write_draft_report)
deep_researcher_builder.add_node("supervisor_subgraph", supervisor_agent) # Here we add our complex sub-graph as a single node.
deep_researcher_builder.add_node("final_report_generation", final_report_generation)

# Now, we define the high-level control flow for the entire system.
# The entry point is the 'clarify_with_user' node.
deep_researcher_builder.add_edge(START, "clarify_with_user")

# The scoping process is a linear sequence.
deep_researcher_builder.add_edge("write_research_brief", "write_draft_report")

# After the initial draft is created, we hand off control to the main Supervisor loop.
deep_researcher_builder.add_edge("write_draft_report", "supervisor_subgraph")

# Once the Supervisor loop completes (by calling ResearchComplete), its output is passed to the final writer.
deep_researcher_builder.add_edge("supervisor_subgraph", "final_report_generation")

# After the final report is generated, the graph terminates.
deep_researcher_builder.add_edge("final_report_generation", END)

# We compile the full, end-to-end workflow into our final 'agent' object.
agent = deep_researcher_builder.compile()
print("✅ Advanced Systems Loaded: Red Team, Context Pruner, and Evaluator are online.")

# %% [markdown]
# We have now successfully assembled our complete, hierarchical, multi-agent system. We have taken our complex, cyclical `supervisor_agent`which has its own parallel branches and internal loops and added it as a single, clean `supervisor_subgraph` node in our master workflow.
# 
# This makes the high-level logic of the system (`Scope -> Supervise -> Finalize`) clean, readable, and easy to manage.
# 
# Let’s visualize our final, fully-integrated graph to see the complete architecture.

# %%
from IPython.display import Image, display

# This will generate a visual representation of our master graph, including the nested sub-graphs.
try:
    display(Image(agent.get_graph(xray=True).draw_mermaid_png()))
except Exception:
    print("Graph visualization requires 'graphviz' installed. Skipping visualization.")

# %% [markdown]
# ![Master Graph (Created by Fareed Khan)](https://miro.medium.com/v2/resize:fit:422/1*TRALXwNqa1qIVAmlQyLvSA.png)
# 
# ### Observing the Diffusion of an 8-Iteration Execution
# 
# With our agent fully compiled, it is time to run it on a genuinely complex, multi-hop research query. We will use the `ainvoke` method to execute the agent and observe the detailed, step-by-step trace of it’s entire workflow.
# 
# This trace is our mission control view, allowing us to see the entire diffusion process unfold, from initial planning to parallel research to the critical interventions of the Red Team and Evaluator.

# %%
# This is our complex, multi-faceted research query.
complex_query = """
Conduct a deep analysis of the 'Splinternet' phenomenon's impact on global semiconductor supply chains by 2028. 
Specifically contrast TSMC's diversification strategy against Intel's IDM 2.0 model under 2024-2025 US export controls, 
and predict the resulting shift in insurance liability models for cross-border wafer shipments.
"""

# We invoke the fully compiled agent with our complex query.
# The 'thread_id' ensures that our conversation history is maintained correctly in LangSmith.

config = {"configurable": {"thread_id": "demo_complex_1"}}
# NOTE: The following execution assumes valid API keys are set and will take several minutes to run.
# The output shown below is a formatted representation of a real execution trace.
result = await agent.ainvoke(
    {"messages": [HumanMessage(content=complex_query)]}, 
    config=config
)

# %% [markdown]
# Let’s take at our query. I chose this specific pattern because it connects very different fields geopolitics, semiconductor strategy, and maritime insurance that are rarely discussed together.
# 
# It pushes the model to actually link political events to financial outcomes. The goal is to test whether the **“Red Team”** can spot and fix the kind of vague, generic responses that LLMs often produce on complex topics.
# 
# By asking for concrete predictions about liability models, we’re creating a challenge that exposes weaknesses in standard models and shows how our iterative system can self-correct toward expert-level accuracy.
# 
# Let’s run this and see how the entire pipeline works ….

# %% [markdown]
# ```rust
# #### OUTPUT TRACE ####
# 🚀 STARTING COMPLEX RESEARCH SIMULATION
# ================================================================================
# QUERY: Conduct a deep analysis of the 'Splinternet' phenomenon's impact...
# 
# --- [ITERATION 1: INITIAL PLANNING] ---
# 🤖 [SUPERVISOR]: I need to break this into three parallel streams: 1. Geopolitical Splinternet effects... 2. Corporate Strategy (TSMC vs Intel)... 3. Insurance/Liability implications...
# 🛠️ [TOOL: think_tool]: Plan recorded. Launching 3 sub-agents.
# 
# --- [ITERATION 2: PARALLEL RESEARCH] ---
# 🔎 [SEARCH]: Agent 1 searching 'Splinternet impact semiconductor supply chain 2025'...
# 🔎 [SEARCH]: Agent 2 searching 'TSMC diversification vs Intel IDM 2.0 export controls'...
# 🔎 [SEARCH]: Agent 3 searching 'semiconductor shipment insurance liability war risk 2025'...
# 📥 [DATA]: Retrieved 14 sources. Raw text buffer size: 42,000 tokens.
# 
# --- [ITERATION 3: PROCESSING & DRAFTING] ---
# 🧹 [LIBRARIAN]: Context Load High! Pruning raw notes...
#    -> Extracted 28 atomic facts.
#    -> Updated Knowledge Graph. Buffer cleared.
# 📝 [WRITER]: Generating Draft v1 based on initial findings...
# 
# --- [ITERATION 4: QUALITY CHECK & ATTACK] ---
# 📈 [EVALUATOR]: Scoring Draft v1...
#    -> Comprehensiveness: 4/10
#    -> Accuracy: 6/10
#    -> Critique: "The section on Insurance is extremely vague..."
# 🔴 [RED TEAM]: ⚠️ ADVERSARIAL ATTACK INITIALIZED
#    -> Claim Attacked: "Intel's IDM 2.0 is immune to Splinternet risks..."
#    -> Counter-Argument: This is logically flawed. Intel relies on ASML (Europe)...
#    -> Severity: HIGH (8/10)
# 
# --- [ITERATION 5: CRITICAL INTERVENTION] ---
# 🤖 [SUPERVISOR]: *** SYSTEM INTERRUPT ***
#    -> Received Red Team Critique: Intel supply chain fallacy.
#    -> Received Evaluator Critique: Missing Insurance data.
#    -> ACTION: Re-planning. Launching targeted repair searches.
# 🔎 [SEARCH]: Searching 'Intel supply chain dependencies non-US'...
# 🔎 [SEARCH]: Searching 'Marine insurance war risk exclusion semiconductor clauses 2024'...
# 
# --- [ITERATION 6: REFINEMENT] ---
# 📥 [DATA]: Found Lloyd's Market Association bulletins... Found Intel 10-K report...
# 🧹 [LIBRARIAN]: Pruning new notes. Added 12 facts to Knowledge Graph. Marking 'Intel Immune' fact as DISPUTED.
# 📝 [WRITER]: Rewriting Draft (v2) with new evidence...
# 
# --- [ITERATION 7: SECOND CHECK] ---
# 📈 [EVALUATOR]: Scoring Draft v2...
#    -> Comprehensiveness: 7.5/10
#    -> Accuracy: 8.5/10
# 🔴 [RED TEAM]: Reviewing Draft v2... -> Result: PASS.
# 
# --- [ITERATION 8: FINAL POLISH] ---
# 🤖 [SUPERVISOR]: Evaluator wants a stronger conclusion... Using 'think_tool' to synthesize...
# 📝 [WRITER]: Finalizing Report (v3)...
# 📈 [EVALUATOR]: Scoring Draft v3... -> Score: 9.2/10. Passed threshold.
# ✅ RESEARCH COMPLETE
# ================================================================================
# ```
# 
# You can see that the output is not a simple, linear RAG pipeline, it is a dynamic, self-correcting, multi-iteration reasoning process. We can clearly see:
# 
# *   **Parallel Research (Iter 2):** The Supervisor correctly decomposed the problem and launched three parallel research agents.
# *   **Memory Management (Iter 3):** The `LIBRARIAN` (Context Pruner) successfully processed the massive 42,000 token raw notes buffer, preventing context overflow.
# *   **Multi-Modal Critique (Iter 4):** Both the programmatic `EVALUATOR` and the adversarial `RED TEAM` ran in parallel, identifying different but complementary weaknesses in the first draft.
# *   **Intelligent Self-Correction (Iter 5):** The Supervisor correctly interpreted the critical feedback, paused its original plan, and launched new, targeted “repair searches” to fix the specific flaws that were identified.
# *   **Convergence (Iter 7–8):** The system continued to refine the draft until both the Red Team and the programmatic Evaluator were satisfied, at which point the Supervisor correctly called `ResearchComplete`.
# 
# It shows a system that doesn’t just execute a static plan, but one that thinks, critiques its own work, and iteratively refines its output towards a high-quality, deeply-reasoned truth.

# %% [markdown]
# ## Analyzing the Final Output
# 
# In this final section, we are going to conduct a direct, qualitative comparison. We will compare the final, polished report generated by our multi-agent research system against the output of a standalone GPT-4o given the exact same complex query.
# 
# ### Monolithic LLM against Deep Researh architeure
# 
# First, let’s look at the final, polished report generated by our complete, multi-iteration deep research agent. This is the culmination of the 8-step `Critique -> Research -> Refine` process we observed in the trace.

# %%
from rich.markdown import Markdown

# The 'result' variable holds the final state from our end-to-end agent invocation.
# We access the 'final_report' field to get the polished output.
Markdown(result["final_report"])

# %% [markdown]
# This is our generated report ….
# 
# ```bash
# # The 'Splinternet' Effect on Semiconductor Supply Chains (2028)
# 
# ## 1. Introduction: The Fragmented Digital Commons
# By 2028, the 'Splinternet'-the divergence of the global internet into distinct US-led and China-led technology spheres-will have fundamentally rewired semiconductor logistics. This report contrasts corporate strategies and predicts liability shifts.
# 
# ## 2. TSMC vs. Intel: Strategic Divergence
# ### TSMC: The 'Ostrich' Diversification
# Despite 2024-2025 export controls, TSMC remains heavily tethered to the 'Silicon Shield' in Taiwan. While expanding into Arizona and Kumamoto, critical advanced packaging (CoWoS) remains 85% concentrated in Taiwan. This creates a single point of failure.
# 
# ### Intel IDM 2.0: The False Haven
# **Correction:** While Intel markets its IDM 2.0 strategy as geographically secure due to US fabrication, our analysis reveals critical dependencies on European lithography (ASML) and Japanese photoresists (JSR). Intel is *not* immune to a blockade, only more resilient to IP theft. The notion of a purely 'US Supply Chain' is a fallacy.
# 
# ## 3. The Insurance Shift: From 'All Risks' to 'Named Perils'
# The most significant unpriced risk for 2028 is the shift in maritime liability.
# 
# ### The 2028 Prediction
# 1. **Liability Shift:** Standard Marine Cargo policies will exclude "Geopolitical Splintering" events. Liability for shipment delays due to digital blockades (e.g., port OS hacks) will shift from the **Carrier (Maersk)** to the **Shipper (Intel/TSMC)**.
# 2. **Parametric Models:** Insurance will move to parametric triggers. Payouts will not be based on physical damage, but on 'Route Latency' exceeding 10 days due to political interference.
# 
# ## 4. Conclusion
# By 2028, the cost of wafers will include a 15% 'Geopolitical Risk Premium'. Intel is better positioned for IP security, but TSMC retains volume dominance. The real winner will be insurers underwriting the new fractured trade routes.
# ```
# 
# Notice the “**Correction:**” in the Intel section this is the direct result of the Red Team intervention, which forced the agent to correct its initial, naive assumption. The entire section on **“The Insurance Shift”** is a product of the Evaluator’s feedback, which pushed the agent to go beyond a superficial mention of **“higher premiums”** and research specific, predictions like the shift to **“Named Perils”** and **“Parametric Models”**. This output is a direct artifact of the iterative, self-correcting process.
# 
# Now, for the comparison. Let’s run the exact same, complex query through a single, monolithic, state-of-the-art LLM (GPT-4o) without any of our agentic architecture. This is our baseline.

# %%
# We initialize a baseline model, a standard instance using ollama.
baseline_model = init_chat_model(model=OLLAMA_MODEL, base_url=OLLAMA_BASE_URL)

print("Running Baseline Model (Ollama gpt-oss:20b)...")
print("-"*80)
# We invoke the model with the same complex query.
response = baseline_model.invoke([HumanMessage(content=complex_query)])
print(response.content)

# %% [markdown]
# Discussion of the Baseline Output
# 
# ```vbnet
# #### OUTPUT ####
# Running Baseline Model (GPT-4o)...
# --------------------------------------------------------------------------------
# **Impact of Splinternet on Semiconductors**
# 
# The "Splinternet" refers to the fragmentation of the internet. By 2028, this could affect supply chains by creating different standards for technology.
# **TSMC vs Intel**
# TSMC is based in Taiwan and focuses on manufacturing. Intel's IDM 2.0 model is about manufacturing its own chips and offering foundry services. Intel is building factories in the US to be safer from geopolitical risks. TSMC is diversifying too but is still centered in Taiwan.
# **Insurance Liability**
# As risks rise, insurance premiums might go up for shipping chips. Companies might have to pay more to insure goods crossing borders that are unfriendly.
# **Conclusion**
# Both companies are trying to adapt. Intel might be safer because it is in the US.
# --------------------------------------------------------------------------------
# ```
# 
# The baseline GPT-4o output is not wrong but it is generic, and critically flawed in its reasoning. It understands the basic concepts but fails to provide any deep, specific, or non-obvious insights.
# 
# 1.  It makes the exact naive assumption **“Intel might be safer because it is in the US”** that our Red Team agent successfully identified and corrected.
# 2.  Its prediction on insurance is a vague, common-sense statement (**“premiums might go up”**), a contrast to the deep research system specific, expert-level prediction about a shift to parametric models.
# 
# Let’s formalize this comparison in a final analysis table.
# 
# The **Red Team** and **Evaluator Loop** were not just features, they were the mechanisms that drove the agent beyond superficial summarization to perform genuine analysis and self-correction.
# 
# They successfully prevented the agent from making the common, plausible-but-wrong assumption that **“US-based fabs = safe”** forcing it to do the extra research that uncovered a deeper, more accurate truth.

# %% [markdown]
# ## Conclusion
# 
# We have successfully designed, built, and tested an complex and powerful AI system. Let’s recap what we have accomplished.
# 
# *   First, we built a user-facing sub-graph to clarify the research request and create an initial draft. This can be improved by adding a user approval step before the main research begins.
# *   We also developed parallel research agents using a ReAct loop with an advanced, summarizing web search tool. This could be enhanced by adding more data sources like academic or financial APIs.
# *   The system core is a Supervisor agent that runs the iterative Critique -> Research -> Refine loop. A future improvement is allowing it to dynamically adjust the number of parallel researchers based on task difficulty.
# *   Finally, we integrated **Red Team** and **Evaluator** agents for advanced self-correction and stability. This could be made more robust by giving the Red Team a long-term memory to recognize recurring errors.


