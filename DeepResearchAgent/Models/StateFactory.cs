namespace DeepResearchAgent.Models;

/// <summary>
/// Factory for creating and initializing state objects in different workflow phases.
/// This ensures consistent state initialization across the diffusion process.
/// </summary>
public class StateFactory
{
    /// <summary>
    /// Create a new AgentState for the start of a research workflow.
    /// </summary>
    public static AgentState CreateAgentState()
    {
        return new AgentState
        {
            Messages = new List<ChatMessage>(),
            ResearchBrief = null,
            SupervisorMessages = new List<ChatMessage>(),
            RawNotes = new List<string>(),
            Notes = new List<string>(),
            DraftReport = string.Empty,
            FinalReport = string.Empty
        };
    }

    /// <summary>
    /// Create a new AgentState from initial user messages.
    /// </summary>
    public static AgentState CreateAgentState(List<ChatMessage> initialMessages)
    {
        return new AgentState
        {
            Messages = new List<ChatMessage>(initialMessages),
            ResearchBrief = null,
            SupervisorMessages = new List<ChatMessage>(),
            RawNotes = new List<string>(),
            Notes = new List<string>(),
            DraftReport = string.Empty,
            FinalReport = string.Empty
        };
    }

    /// <summary>
    /// Create a new SupervisorState for the diffusion loop.
    /// </summary>
    public static SupervisorState CreateSupervisorState()
    {
        return new SupervisorState
        {
            SupervisorMessages = new List<ChatMessage>(),
            ResearchBrief = string.Empty,
            DraftReport = string.Empty,
            RawNotes = new List<string>(),
            KnowledgeBase = new List<FactState>(),
            ResearchIterations = 0,
            ActiveCritiques = new List<CritiqueState>(),
            QualityHistory = new List<QualityMetric>(),
            NeedsQualityRepair = false
        };
    }

    /// <summary>
    /// Create a new SupervisorState from a research brief and initial draft.
    /// </summary>
    public static SupervisorState CreateSupervisorState(string researchBrief, string draftReport, List<ChatMessage> initialMessages)
    {
        return new SupervisorState
        {
            SupervisorMessages = new List<ChatMessage>(initialMessages),
            ResearchBrief = researchBrief,
            DraftReport = draftReport,
            RawNotes = new List<string>(),
            KnowledgeBase = new List<FactState>(),
            ResearchIterations = 0,
            ActiveCritiques = new List<CritiqueState>(),
            QualityHistory = new List<QualityMetric>(),
            NeedsQualityRepair = false
        };
    }

    /// <summary>
    /// Create a new ResearcherState for a focused research task.
    /// </summary>
    public static ResearcherState CreateResearcherState()
    {
        return new ResearcherState
        {
            ResearcherMessages = new List<ChatMessage>(),
            ToolCallIterations = 0,
            ResearchTopic = string.Empty,
            CompressedResearch = string.Empty,
            RawNotes = new List<string>()
        };
    }

    /// <summary>
    /// Create a new ResearcherState for a specific research topic.
    /// </summary>
    public static ResearcherState CreateResearcherState(string topic)
    {
        return new ResearcherState
        {
            ResearcherMessages = new List<ChatMessage>(),
            ToolCallIterations = 0,
            ResearchTopic = topic,
            CompressedResearch = string.Empty,
            RawNotes = new List<string>()
        };
    }

    /// <summary>
    /// Create a new ResearcherState from an initial message about the research topic.
    /// </summary>
    public static ResearcherState CreateResearcherState(string topic, List<ChatMessage> initialMessages)
    {
        return new ResearcherState
        {
            ResearcherMessages = new List<ChatMessage>(initialMessages),
            ToolCallIterations = 0,
            ResearchTopic = topic,
            CompressedResearch = string.Empty,
            RawNotes = new List<string>()
        };
    }

    /// <summary>
    /// Create a new CritiqueState from feedback.
    /// </summary>
    public static CritiqueState CreateCritique(string author, string concern, int severity, bool addressed = false)
    {
        return new CritiqueState
        {
            Author = author,
            Concern = concern,
            Severity = severity,
            Addressed = addressed
        };
    }

    /// <summary>
    /// Create a new QualityMetric from evaluation results.
    /// </summary>
    public static QualityMetric CreateQualityMetric(float score, string feedback, int iteration)
    {
        return new QualityMetric
        {
            Score = score,
            Feedback = feedback,
            Iteration = iteration
        };
    }

    /// <summary>
    /// Create a new FactState from extracted knowledge.
    /// </summary>
    public static FactState CreateFact(string content, string sourceUrl, int confidenceScore, bool isDisputed = false)
    {
        return new FactState
        {
            Content = content,
            SourceUrl = sourceUrl,
            ConfidenceScore = Math.Clamp(confidenceScore, 1, 100),
            IsDisputed = isDisputed
        };
    }

    /// <summary>
    /// Clone an AgentState (shallow copy of lists).
    /// </summary>
    public static AgentState CloneAgentState(AgentState original)
    {
        return new AgentState
        {
            Messages = new List<ChatMessage>(original.Messages),
            ResearchBrief = original.ResearchBrief,
            SupervisorMessages = new List<ChatMessage>(original.SupervisorMessages),
            RawNotes = new List<string>(original.RawNotes),
            Notes = new List<string>(original.Notes),
            DraftReport = original.DraftReport,
            FinalReport = original.FinalReport
        };
    }

    /// <summary>
    /// Clone a SupervisorState (shallow copy of lists).
    /// </summary>
    public static SupervisorState CloneSupervisorState(SupervisorState original)
    {
        return new SupervisorState
        {
            SupervisorMessages = new List<ChatMessage>(original.SupervisorMessages),
            ResearchBrief = original.ResearchBrief,
            DraftReport = original.DraftReport,
            RawNotes = new List<string>(original.RawNotes),
            KnowledgeBase = new List<FactState>(original.KnowledgeBase),
            ResearchIterations = original.ResearchIterations,
            ActiveCritiques = new List<CritiqueState>(original.ActiveCritiques),
            QualityHistory = new List<QualityMetric>(original.QualityHistory),
            NeedsQualityRepair = original.NeedsQualityRepair
        };
    }

    /// <summary>
    /// Clone a ResearcherState (shallow copy of lists).
    /// </summary>
    public static ResearcherState CloneResearcherState(ResearcherState original)
    {
        return new ResearcherState
        {
            ResearcherMessages = new List<ChatMessage>(original.ResearcherMessages),
            ToolCallIterations = original.ToolCallIterations,
            ResearchTopic = original.ResearchTopic,
            CompressedResearch = original.CompressedResearch,
            RawNotes = new List<string>(original.RawNotes)
        };
    }
}
