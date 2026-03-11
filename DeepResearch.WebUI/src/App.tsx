import { useState, useEffect } from 'react'
import Sidebar from '@components/Sidebar'
import RightSidebar from '@components/RightSidebar'
import ChatDialog from '@components/ChatDialog'
import ChatDialogDivider from '@components/ChatDialogDivider'
import ResearchStreamingPanel from '@components/ResearchStreamingPanel'
import { ThemeProvider } from '@contexts/ThemeContext'
import { apiService } from '@services/api'
import { useChatDialogWidth } from '@hooks/useChatDialogWidth'
import { Menu, X } from 'lucide-react'

function App() {
  const [currentSessionId, setCurrentSessionId] = useState<string | null>(null)
  const [isInitializing, setIsInitializing] = useState(true)
  const [viewMode, setViewMode] = useState<'chat' | 'research'>('chat')
  const [debugError, setDebugError] = useState<string>('')
  
  // Sidebar states
  const [sidebarOpen, setSidebarOpen] = useState(false)        // Start COLLAPSED (icon bar)
  const [sidebarTacked, setSidebarTacked] = useState(true)    // Tacked/Untacked (flyout)
  const [rightSidebarOpen, setRightSidebarOpen] = useState(false)  // Right sidebar collapsed
  const [rightSidebarTacked, setRightSidebarTacked] = useState(true)  // Right sidebar tacked
  const [rightSidebarView, setRightSidebarView] = useState<'config' | 'info'>('info')  // Which view to show
  
  // Chat dialog resize - maintain fixed 66% width regardless of sidebars
  const { width: chatWidth, isDragging, startResizing } = useChatDialogWidth({
    initialWidth: 66,
    minWidth: 66,  // Fixed width
    maxWidth: 66,  // Fixed width
    onResize: () => {
      // Don't resize chat when sidebars collapse
    }
  })

  // Initialize session on mount
  useEffect(() => {
    const initializeSession = async () => {
      try {
        console.log('[App] ===== SESSION INITIALIZATION START =====')

        // Try to load last session from localStorage
        const lastSessionId = localStorage.getItem('lastSessionId')
        console.log('[App] lastSessionId from localStorage:', lastSessionId)

        if (lastSessionId) {
          // Check if session still exists on server
          console.log('[App] Checking if last session still exists:', lastSessionId)
          try {
            const exists = await apiService.sessionExists(lastSessionId)
            if (exists) {
              console.log('[App] ✅ Session exists on server!')
              // Load the history to verify it works
              const history = await apiService.getChatHistory(lastSessionId)
              console.log('[App] ✅ History loaded! Has', history.length, 'messages')
              setCurrentSessionId(lastSessionId)
              setDebugError('')
              console.log('[App] ===== SESSION INITIALIZATION COMPLETE (restored) =====')
              setIsInitializing(false)
              return
            } else {
              console.log('[App] Session no longer exists on server')
            }
          } catch (verifyErr) {
            console.warn('[App] ⚠️  Error verifying session:', verifyErr instanceof Error ? verifyErr.message : verifyErr)
          }
          localStorage.removeItem('lastSessionId')
        }

        // Create new session if no valid session exists
        console.log('[App] Creating new session...')
        const session = await apiService.createSession(`New Chat ${new Date().toLocaleTimeString()}`)
        console.log('[App] ✅ New session created:', { id: session.id, title: session.title })

        if (!session.id) {
          throw new Error('Session created but missing id field')
        }

        setCurrentSessionId(session.id)
        setDebugError('')
        console.log('[App] ===== SESSION INITIALIZATION COMPLETE (new session) =====')
      } catch (err) {
        console.error('[App] ❌ FATAL: Failed to initialize session:', err)
        const errorMsg = err instanceof Error ? err.message : String(err)
        console.error('[App] Error message:', errorMsg)
        console.error('[App] Error stack:', err instanceof Error ? err.stack : 'N/A')
        setDebugError(`Initialization failed: ${errorMsg}`)

        // Create a temporary offline session ID as last resort
        const offlineId = 'offline-' + Date.now()
        console.log('[App] Creating offline fallback session:', offlineId)
        setCurrentSessionId(offlineId)
        console.log('[App] ===== SESSION INITIALIZATION COMPLETE (offline fallback) =====')
      } finally {
        console.log('[App] Setting isInitializing to false')
        setIsInitializing(false)
      }
    }

    initializeSession()
  }, [])

  // Save current session to localStorage
  useEffect(() => {
    if (currentSessionId && !currentSessionId.startsWith('offline-')) {
      localStorage.setItem('lastSessionId', currentSessionId)
    }
  }, [currentSessionId])

  const handleNewChat = async () => {
    try {
      const session = await apiService.createSession(`New Chat ${new Date().toLocaleTimeString()}`)
      setCurrentSessionId(session.id)
    } catch (err) {
      console.error('Failed to create session:', err)
      // Create offline session
      setCurrentSessionId('offline-' + Date.now())
    }
  }

  const handleSelectSession = (sessionId: string) => {
    setCurrentSessionId(sessionId)
  }

  return (
    <ThemeProvider>
      <div className="flex h-screen bg-gray-50 dark:bg-gray-900">
        {/* Left Sidebar */}
        <Sidebar 
          onNewChat={handleNewChat} 
          currentSessionId={currentSessionId}
          onSelectSession={handleSelectSession}
          sidebarOpen={sidebarOpen}
          setSidebarOpen={setSidebarOpen}
          sidebarTacked={sidebarTacked}
          setSidebarTacked={setSidebarTacked}
          rightSidebarOpen={rightSidebarOpen}
          setRightSidebarOpen={setRightSidebarOpen}
          rightSidebarTacked={rightSidebarTacked}
          setRightSidebarTacked={setRightSidebarTacked}
          rightSidebarView={rightSidebarView}
          setRightSidebarView={setRightSidebarView}
        />

        {/* Middle Content - Chat area fills space between sidebars */}
        <div className={`flex-1 flex flex-col h-screen transition-all duration-300`}>
          {isInitializing ? (
            <div className="flex items-center justify-center h-full">
              <div className="text-center">
                <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto mb-4"></div>
                <p className="text-gray-600 dark:text-gray-400">Initializing...</p>
              </div>
            </div>
          ) : currentSessionId ? (
            <div className="flex h-full gap-0 overflow-hidden flex-col">
              {/* Error Banner */}
              {debugError && (
                <div className="bg-red-100 dark:bg-red-900 border border-red-400 dark:border-red-700 text-red-700 dark:text-red-200 px-4 py-3 rounded-none">
                  <div className="flex justify-between items-center">
                    <span><strong>⚠️ Initialization Warning:</strong> {debugError}</span>
                    <button 
                      onClick={() => setDebugError('')}
                      className="text-red-700 dark:text-red-200 hover:text-red-900 dark:hover:text-red-100"
                    >
                      ✕
                    </button>
                  </div>
                </div>
              )}
              {/* Main Content */}
              <div className="flex h-full gap-0 overflow-hidden flex-1">
                {/* Chat Area - Fills available space between sidebars */}
                <div 
                  className="flex flex-col overflow-hidden flex-1"
                >
                  {/* View Mode Selector */}
                  <div className="flex gap-2 px-4 py-3 border-b border-gray-200 dark:border-gray-700">
                    <button
                      onClick={() => setViewMode('chat')}
                      className={`px-3 py-1.5 rounded text-sm font-medium transition-colors ${
                        viewMode === 'chat'
                          ? 'bg-blue-600 text-white'
                          : 'bg-gray-100 dark:bg-gray-800 text-gray-700 dark:text-gray-300 hover:bg-gray-200 dark:hover:bg-gray-700'
                      }`}
                    >
                      💬 Chat
                    </button>
                    <button
                      onClick={() => setViewMode('research')}
                      className={`px-3 py-1.5 rounded text-sm font-medium transition-colors ${
                        viewMode === 'research'
                          ? 'bg-blue-600 text-white'
                          : 'bg-gray-100 dark:bg-gray-800 text-gray-700 dark:text-gray-300 hover:bg-gray-200 dark:hover:bg-gray-700'
                      }`}
                    >
                      🔬 Research
                    </button>
                  </div>

                  {/* Chat or Research View */}
                  <div className="flex-1 overflow-hidden">
                    {viewMode === 'chat' ? (
                      <ChatDialog sessionId={currentSessionId} />
                    ) : (
                      <ResearchStreamingPanel />
                    )}
                  </div>
                </div>

                {/* Right Sidebar - Takes remaining space */}
                <RightSidebar
                  sessionId={currentSessionId}
                  rightSidebarOpen={rightSidebarOpen}
                  setRightSidebarOpen={setRightSidebarOpen}
                  rightSidebarTacked={rightSidebarTacked}
                  setRightSidebarTacked={setRightSidebarTacked}
                  rightSidebarView={rightSidebarView}
                  setRightSidebarView={setRightSidebarView}
                />
              </div>
            </div>
          ) : (
            <div className="flex items-center justify-center h-full">
              <div className="text-center">
                <p className="text-gray-600 dark:text-gray-400 mb-4">No session available</p>
                <button
                  onClick={handleNewChat}
                  className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors"
                >
                  Start New Chat
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </ThemeProvider>
  )
}

export default App
