import { create } from 'zustand'

export interface DebugMessage {
  id: string
  timestamp: Date
  direction: 'sent' | 'received'
  type: 'message' | 'state' | 'error' | 'api_call'
  data: any
  endpoint?: string
  statusCode?: number
}

interface DebugStore {
  messages: DebugMessage[]
  isConsoleVisible: boolean
  consoleHeight: number
  activeTab: 'messages' | 'state' | 'api_calls'
  
  // Actions
  addDebugMessage: (message: Omit<DebugMessage, 'id' | 'timestamp'>) => void
  clearMessages: () => void
  toggleConsole: () => void
  setConsoleHeight: (height: number) => void
  setActiveTab: (tab: 'messages' | 'state' | 'api_calls') => void
}

export const useDebugStore = create<DebugStore>((set) => ({
  messages: [],
  isConsoleVisible: false,
  consoleHeight: 30, // percentage
  activeTab: 'messages',

  addDebugMessage: (message) =>
    set((state) => ({
      messages: [
        ...state.messages,
        {
          ...message,
          id: crypto.randomUUID(),
          timestamp: new Date(),
        },
      ],
    })),

  clearMessages: () => set({ messages: [] }),

  toggleConsole: () =>
    set((state) => ({ isConsoleVisible: !state.isConsoleVisible })),

  setConsoleHeight: (height) =>
    set({ consoleHeight: Math.max(10, Math.min(70, height)) }),

  setActiveTab: (tab) => set({ activeTab: tab }),
}))
