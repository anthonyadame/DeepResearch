namespace DeepResearchAgent.Models;

/// <summary>
/// Manages state transitions and routing decisions similar to LangGraph's Command[Literal[...]] pattern.
/// Enables declarative control flow for multi-agent workflows.
/// </summary>
public abstract class StateTransition
{
    /// <summary>
    /// The next node/workflow to execute (target state).
    /// </summary>
    public abstract string NextNode { get; }

    /// <summary>
    /// Data to merge into the target state.
    /// </summary>
    public virtual AgentState? UpdateData { get; } = null;

    /// <summary>
    /// Whether this is a terminal transition (ends the workflow).
    /// </summary>
    public virtual bool IsTerminal { get; } = false;
}

/// <summary>
/// Transition to a specific named node with state updates.
/// </summary>
public class NodeTransition : StateTransition
{
    private readonly string _nextNode;
    private readonly AgentState? _updateData;

    public NodeTransition(string nextNode, AgentState? updateData = null)
    {
        _nextNode = nextNode ?? throw new ArgumentNullException(nameof(nextNode));
        _updateData = updateData;
    }

    public override string NextNode => _nextNode;
    public override AgentState? UpdateData => _updateData;
}

/// <summary>
/// Conditional transition that routes to different nodes based on state.
/// </summary>
public class ConditionalTransition : StateTransition
{
    private readonly Func<AgentState, string> _router;
    private readonly string _fallbackNode;

    public ConditionalTransition(Func<AgentState, string> router, string fallbackNode = "end")
    {
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _fallbackNode = fallbackNode;
    }

    public override string NextNode => _fallbackNode;

    /// <summary>
    /// Resolve the actual next node based on current state.
    /// </summary>
    public string ResolveNextNode(AgentState state)
    {
        try
        {
            return _router(state) ?? _fallbackNode;
        }
        catch
        {
            return _fallbackNode;
        }
    }
}

/// <summary>
/// Parallel transition that routes to multiple nodes simultaneously.
/// </summary>
public class ParallelTransition : StateTransition
{
    private readonly string[] _nextNodes;

    public ParallelTransition(params string[] nextNodes)
    {
        if (nextNodes == null || nextNodes.Length == 0)
            throw new ArgumentException("At least one node must be specified", nameof(nextNodes));

        _nextNodes = nextNodes;
    }

    public override string NextNode => _nextNodes[0];

    /// <summary>
    /// Get all nodes to execute in parallel.
    /// </summary>
    public IReadOnlyList<string> GetParallelNodes() => _nextNodes.AsReadOnly();
}

/// <summary>
/// Terminal transition that ends the workflow.
/// </summary>
public class EndTransition : StateTransition
{
    public const string END_NODE = "__end__";

    public EndTransition(AgentState? finalData = null)
    {
        _updateData = finalData;
    }

    public override string NextNode => END_NODE;
    public override bool IsTerminal => true;
    private readonly AgentState? _updateData;
    public override AgentState? UpdateData => _updateData;
}

/// <summary>
/// Routes state transitions throughout the workflow execution, handling both
/// sequential and parallel node execution (similar to LangGraph's routing).
/// </summary>
public class StateTransitionRouter
{
    private readonly Dictionary<string, List<Func<AgentState, StateTransition>>> _routes;
    private readonly string _startNode;
    private readonly string _endNode;

    public StateTransitionRouter(string startNode = "start", string endNode = "__end__")
    {
        _routes = new Dictionary<string, List<Func<AgentState, StateTransition>>>();
        _startNode = startNode;
        _endNode = endNode;
    }

    /// <summary>
    /// Register a deterministic edge from one node to another.
    /// </summary>
    public void RegisterEdge(string fromNode, string toNode)
    {
        RegisterTransition(fromNode, state => new NodeTransition(toNode));
    }

    /// <summary>
    /// Register a conditional edge based on state evaluation.
    /// </summary>
    public void RegisterConditionalEdge(string fromNode, Func<AgentState, string> router, string fallbackNode = "__end__")
    {
        RegisterTransition(fromNode, state => new ConditionalTransition(router, fallbackNode));
    }

    /// <summary>
    /// Register a parallel edge that routes to multiple nodes.
    /// </summary>
    public void RegisterParallelEdge(string fromNode, params string[] toNodes)
    {
        RegisterTransition(fromNode, state => new ParallelTransition(toNodes));
    }

    /// <summary>
    /// Register a custom transition handler for a node.
    /// </summary>
    public void RegisterTransition(string fromNode, Func<AgentState, StateTransition> transitionHandler)
    {
        if (!_routes.ContainsKey(fromNode))
            _routes[fromNode] = new List<Func<AgentState, StateTransition>>();

        _routes[fromNode].Add(transitionHandler);
    }

    /// <summary>
    /// Get the next transition for a given node and state.
    /// </summary>
    public StateTransition? GetNextTransition(string currentNode, AgentState state)
    {
        if (!_routes.ContainsKey(currentNode))
            return new EndTransition(state);

        var handlers = _routes[currentNode];
        if (handlers.Count == 0)
            return new EndTransition(state);

        // Execute the last registered handler (allows overrides)
        return handlers[handlers.Count - 1](state);
    }

    /// <summary>
    /// Get the starting node for workflow execution.
    /// </summary>
    public string GetStartNode() => _startNode;

    /// <summary>
    /// Check if a node is a terminal node.
    /// </summary>
    public bool IsTerminal(string nodeName) => nodeName == _endNode;

    /// <summary>
    /// Get all registered nodes in the workflow.
    /// </summary>
    public IReadOnlyCollection<string> GetRegisteredNodes() => new List<string>(_routes.Keys);
}

/// <summary>
/// Supervisor-specific state transitions for the diffusion loop.
/// </summary>
public abstract class SupervisorStateTransition
{
    public abstract string NextNode { get; }
    public virtual SupervisorState? UpdateData { get; } = null;
    public virtual bool IsTerminal { get; } = false;
}

/// <summary>
/// Routes supervisor state transitions through the diffusion process.
/// Handles supervisor brain → tools → red team/context pruner loops.
/// </summary>
public class SupervisorTransitionRouter
{
    private readonly Dictionary<string, Func<SupervisorState, SupervisorStateTransition>> _routes;
    private readonly string _startNode;
    private readonly string _endNode;

    public SupervisorTransitionRouter(string startNode = "supervisor", string endNode = "__end__")
    {
        _routes = new Dictionary<string, Func<SupervisorState, SupervisorStateTransition>>();
        _startNode = startNode;
        _endNode = endNode;
    }

    /// <summary>
    /// Register an edge for supervisor state transition.
    /// </summary>
    public void RegisterEdge(string fromNode, string toNode)
    {
        _routes[fromNode] = state => new SupervisorNodeTransition(toNode);
    }

    /// <summary>
    /// Register a conditional edge for supervisor routing.
    /// </summary>
    public void RegisterConditionalEdge(string fromNode, Func<SupervisorState, string> router)
    {
        _routes[fromNode] = state => new SupervisorNodeTransition(router(state) ?? _endNode);
    }

    /// <summary>
    /// Register parallel routing from supervisor tools to red team and context pruner.
    /// </summary>
    public void RegisterParallelEdge(string fromNode, params string[] toNodes)
    {
        _routes[fromNode] = state => new SupervisorParallelTransition(toNodes);
    }

    /// <summary>
    /// Get the next supervisor transition.
    /// </summary>
    public SupervisorStateTransition? GetNextTransition(string currentNode, SupervisorState state)
    {
        if (!_routes.ContainsKey(currentNode))
            return new SupervisorEndTransition(state);

        return _routes[currentNode](state);
    }

    public string GetStartNode() => _startNode;
    public bool IsTerminal(string nodeName) => nodeName == _endNode;
}

/// <summary>
/// Simple supervisor node transition.
/// </summary>
public class SupervisorNodeTransition : SupervisorStateTransition
{
    private readonly string _nextNode;

    public SupervisorNodeTransition(string nextNode, SupervisorState? updateData = null)
    {
        _nextNode = nextNode ?? throw new ArgumentNullException(nameof(nextNode));
        _updateData = updateData;
    }

    public override string NextNode => _nextNode;
    private readonly SupervisorState? _updateData;
    public override SupervisorState? UpdateData => _updateData;
}

/// <summary>
/// Parallel supervisor transition.
/// </summary>
public class SupervisorParallelTransition : SupervisorStateTransition
{
    private readonly string[] _nextNodes;

    public SupervisorParallelTransition(params string[] nextNodes)
    {
        _nextNodes = nextNodes ?? throw new ArgumentException("Nodes required", nameof(nextNodes));
    }

    public override string NextNode => _nextNodes[0];
    public IReadOnlyList<string> GetParallelNodes() => _nextNodes.AsReadOnly();
}

/// <summary>
/// Terminal supervisor transition.
/// </summary>
public class SupervisorEndTransition : SupervisorStateTransition
{
    public const string END_NODE = "__end__";

    public SupervisorEndTransition(SupervisorState? finalData = null)
    {
        _updateData = finalData;
    }

    public override string NextNode => END_NODE;
    public override bool IsTerminal => true;
    private readonly SupervisorState? _updateData;
    public override SupervisorState? UpdateData => _updateData;
}
