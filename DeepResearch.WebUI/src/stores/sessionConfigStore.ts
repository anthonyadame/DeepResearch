import { create } from 'zustand'
import { persist } from 'zustand/middleware'

export interface ModelInfo {
  id: string
  name: string
  contextWindow?: number
  tokensUsed?: number
  tokensAvailable?: number
}

export interface SessionConfig {
  // Model Settings
  selectedModel: string
  temperature: number
  maxTokens: number
  
  // Search Settings
  webSearchProvider: string
  
  // System Settings
  systemMessage: string
  
  // Stream Settings
  streamEnabled: boolean
  streamDeltaChunk: number
  
  // Interaction Settings
  askFollowUpQuestions: boolean
  
  // Token Tracking
  sessionTokensUsed: number
  modelTokensUsed: Record<string, number>
}

interface SessionConfigStore {
  // State
  configs: Record<string, SessionConfig>
  currentSessionId: string | null
  availableModels: ModelInfo[]
  isPanelVisible: boolean
  
  // Actions
  setCurrentSession: (sessionId: string) => void
  getSessionConfig: (sessionId: string) => SessionConfig
  updateSessionConfig: (sessionId: string, updates: Partial<SessionConfig>) => void
  resetSessionConfig: (sessionId: string) => void
  togglePanel: () => void
  setPanelVisible: (visible: boolean) => void
  setAvailableModels: (models: ModelInfo[]) => void
  updateModelTokenUsage: (sessionId: string, model: string, tokens: number) => void
  getDefaultConfig: () => SessionConfig
}

const DEFAULT_CONFIG: SessionConfig = {
  selectedModel: 'gpt-4',
  temperature: 0.7,
  maxTokens: 4000,
  webSearchProvider: 'searxng',
  systemMessage: '',
  streamEnabled: true,
  streamDeltaChunk: 50,
  askFollowUpQuestions: true,
  sessionTokensUsed: 0,
  modelTokensUsed: {},
}

export const useSessionConfigStore = create<SessionConfigStore>()(
  persist(
    (set, get) => ({
      configs: {},
      currentSessionId: null,
      availableModels: [],
      isPanelVisible: false,

      setCurrentSession: (sessionId) => {
        set({ currentSessionId: sessionId })
        // Initialize config if it doesn't exist
        const configs = get().configs
        if (!configs[sessionId]) {
          set({
            configs: {
              ...configs,
              [sessionId]: { ...DEFAULT_CONFIG },
            },
          })
        }
      },

      getSessionConfig: (sessionId) => {
        const configs = get().configs
        return configs[sessionId] || { ...DEFAULT_CONFIG }
      },

      updateSessionConfig: (sessionId, updates) => {
        set((state) => ({
          configs: {
            ...state.configs,
            [sessionId]: {
              ...get().getSessionConfig(sessionId),
              ...updates,
            },
          },
        }))
      },

      resetSessionConfig: (sessionId) => {
        set((state) => ({
          configs: {
            ...state.configs,
            [sessionId]: { ...DEFAULT_CONFIG },
          },
        }))
      },

      togglePanel: () => {
        set((state) => ({ isPanelVisible: !state.isPanelVisible }))
      },

      setPanelVisible: (visible) => {
        set({ isPanelVisible: visible })
      },

      setAvailableModels: (models) => {
        set({ availableModels: models })
      },

      updateModelTokenUsage: (sessionId, model, tokens) => {
        const config = get().getSessionConfig(sessionId)
        set((state) => ({
          configs: {
            ...state.configs,
            [sessionId]: {
              ...config,
              sessionTokensUsed: config.sessionTokensUsed + tokens,
              modelTokensUsed: {
                ...config.modelTokensUsed,
                [model]: (config.modelTokensUsed[model] || 0) + tokens,
              },
            },
          },
        }))
      },

      getDefaultConfig: () => ({ ...DEFAULT_CONFIG }),
    }),
    {
      name: 'session-config-storage',
      partialize: (state) => ({ configs: state.configs }),
    }
  )
)
