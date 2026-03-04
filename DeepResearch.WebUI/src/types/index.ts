// API Response Types
export interface ChatMessage {
  id?: string
  role: 'user' | 'assistant' | 'system'
  content: string
  timestamp?: string
  metadata?: any
}

export interface ChatSession {
  id: string
  title: string
  messages: ChatMessage[]
  createdAt: Date
  updatedAt: Date
  config?: ResearchConfig | SessionConfig
}

export interface SessionConfig {
  selectedModel?: string
  temperature?: number
  maxTokens?: number
  webSearchProvider?: string
  systemMessage?: string
  streamEnabled?: boolean
  streamDeltaChunk?: number
  askFollowUpQuestions?: boolean
}

export interface ModelInfo {
  id: string
  name: string
  contextWindow?: number
  tokensUsed?: number
  tokensAvailable?: number
}

export interface SupervisorState {
  draftReport?: string
  supervisorMessages?: ChatMessage[]
  rawNotes?: string[]
}

export interface AgentState {
  messages: ChatMessage[]
  researchBrief?: string
  draftReport?: string
  finalReport?: string
  supervisorMessages: ChatMessage[]
  rawNotes: string[]
  supervisorState?: SupervisorState
  needsQualityRepair: boolean
}

export interface ChatStepRequest {
  currentState: AgentState
  userResponse?: string
  config?: unknown
}

export interface ChatStepResponse {
  updatedState: AgentState
  displayContent: string
  currentStep: number
  clarificationQuestion?: string
  isComplete: boolean
  statusMessage: string
  metrics?: Record<string, unknown>
}

export interface ResearchConfig {
  language?: string
  llmModels: string[]
  includedWebsites: string[]
  excludedWebsites: string[]
  topics: string[]
  maxDepth?: number
  timeoutSeconds?: number
}

export interface WebSearchTool {
  id: string
  name: string
  enabled: boolean
  type: 'web' | 'semantic' | 'local'
}

export interface ApiResponse<T> {
  success: boolean
  data?: T
  error?: string
  timestamp: Date
}

/**
 * StreamState represents real-time progress updates from the research pipeline
 * @property status - Current phase status (JSON string)
 * @property researchId - Unique research request identifier
 * @property userQuery - Original user input query
 * @property briefPreview - First 150 chars of research brief
 * @property researchBrief - Full structured research plan
 * @property draftReport - Initial "noisy" draft report
 * @property refinedSummary - Supervisor-refined output
 * @property finalReport - Polished final report
 * @property supervisorUpdate - Live refinement step message
 * @property supervisorUpdateCount - Count of refinement iterations
 */
export interface StreamState {
  status?: string
  researchId?: string
  userQuery?: string
  briefPreview?: string
  researchBrief?: string
  draftReport?: string
  refinedSummary?: string
  finalReport?: string
  supervisorUpdate?: string
  supervisorUpdateCount?: number
}

/**
 * Progress indicator for research pipeline phases
 */
export interface ResearchProgress {
  phase: 'clarify' | 'brief' | 'draft' | 'supervisor' | 'final' | 'complete' | 'error'
  percentage: number
  message: string
  content?: string
}
