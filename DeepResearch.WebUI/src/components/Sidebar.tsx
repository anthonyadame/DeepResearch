import { useState } from 'react'
import { Menu, Plus, MessageSquare, X, History, ChevronLeft, Settings, Palette, Pin, PanelLeftOpen, PanelLeftClose } from 'lucide-react'
import ChatHistoryPanel from './ChatHistoryPanel'
import ThemeDialog from './ThemeDialog'

interface SidebarProps {
  onNewChat: () => void
  currentSessionId: string | null
  onSelectSession: (sessionId: string) => void
  sidebarOpen?: boolean
  setSidebarOpen?: (open: boolean) => void
  sidebarTacked?: boolean
  setSidebarTacked?: (tacked: boolean) => void
  rightSidebarOpen?: boolean
  setRightSidebarOpen?: (open: boolean) => void
}

type SidebarView = 'main' | 'history' | 'config' | 'messages'

export default function Sidebar({ 
  onNewChat, 
  currentSessionId, 
  onSelectSession,
  sidebarOpen = true,
  setSidebarOpen,
  sidebarTacked = true,
  setSidebarTacked,
  rightSidebarOpen = false,
  setRightSidebarOpen
}: SidebarProps) {
  const [currentView, setCurrentView] = useState<SidebarView>('main')
  const [showThemeDialog, setShowThemeDialog] = useState(false)

  const handleBackToMain = () => {
    setCurrentView('main')
  }

  // Sample API messages for demo
  const apiMessages = [
    '📥 [CHAT] Request received',
    '📝 [STORAGE] Adding user message to session',
    '✓ Loaded session',
    'Workflow started (DeepResearchAgent.Workflows.MasterWorkflow[0])',
    'Step 1: Clarifying user intent',
    'User clarification needed'
  ]

  // sidebarOpen=true && sidebarTacked=true   → Full sidebar (docked)
  // sidebarOpen=false && sidebarTacked=true  → Icon bar (collapsed, docked)
  // sidebarOpen=true && sidebarTacked=false  → Flyout drawer (overlays)
  // sidebarOpen=false && sidebarTacked=false → Icon bar + flyout on click

  return (
    <>
      {/* Flyout overlay (when untacked and expanded) */}
      {!sidebarTacked && sidebarOpen && (
        <div
          className="fixed inset-0 bg-black bg-opacity-50 z-20"
          onClick={() => setSidebarTacked?.(true)}
        />
      )}

      {/* Collapsed Icon Bar OR Expanded Sidebar OR Flyout Drawer */}
      <aside
        className={`transition-all duration-300 h-screen bg-gray-900 text-white z-40 flex flex-col flex-shrink-0 ${
          // Full sidebar (expanded & tacked)
          sidebarOpen && sidebarTacked
            ? 'relative w-64'
            // Icon bar (collapsed & tacked)
            : !sidebarOpen && sidebarTacked
            ? 'relative w-16 items-center'
            // Flyout drawer (expanded & untacked)
            : sidebarOpen && !sidebarTacked
            ? 'fixed left-0 top-0 w-64 shadow-lg'
            // Icon bar that opens flyout (collapsed & untacked)
            : 'relative w-16 items-center'
        }`}
      >
        {/* FULL SIDEBAR VIEW */}
        {sidebarOpen && sidebarTacked && (
          <>
            {/* Header */}
            <div className="p-4 border-b border-gray-700 flex items-center justify-between">
              <h1 className="text-xl font-bold">Deep Research</h1>
              
              <div className="flex items-center gap-2">
                {/* Tack Icon (always visible when expanded) */}
                <button
                  onClick={() => setSidebarTacked?.(false)}
                  className="p-1 hover:bg-gray-700 rounded transition-colors"
                  title="Overlay mode"
                >
                  <Pin className="w-5 h-5 fill-blue-400 text-blue-400" />
                </button>
                
                {/* Collapse button */}
                <button
                  onClick={() => setSidebarOpen?.(false)}
                  className="p-1 hover:bg-gray-700 rounded transition-colors"
                  title="Collapse"
                >
                  <PanelLeftClose className="w-5 h-5" />
                </button>
              </div>
            </div>

            {/* Full content */}
            {currentView === 'main' && (
              <>
                <div className="p-4">
                  <button
                    onClick={onNewChat}
                    className="w-full flex items-center gap-2 px-4 py-3 bg-blue-600 hover:bg-blue-700 rounded-lg transition-colors justify-center"
                    title="New chat"
                  >
                    <Plus className="w-5 h-5" />
                    <span>New Chat</span>
                  </button>
                </div>

                <nav className="flex-1 px-2 space-y-1">
                  <SidebarItem 
                    icon={<Settings className="w-5 h-5" />} 
                    label="Session Config" 
                    onClick={() => setCurrentView('config')}
                    title="Session configuration"
                  />
                  <SidebarItem 
                    icon={<MessageSquare className="w-5 h-5" />} 
                    label="Messages" 
                    onClick={() => setCurrentView('messages')}
                    title="Session info messages"
                  />
                  <div className="border-t border-gray-700 my-2"></div>
                  <SidebarItem 
                    icon={<History className="w-5 h-5" />} 
                    label="Chat History" 
                    onClick={() => setCurrentView('history')}
                  />
                  <SidebarItem 
                    icon={<Palette className="w-5 h-5" />} 
                    label="Themes" 
                    onClick={() => setShowThemeDialog(true)}
                  />
                </nav>
              </>
            )}

            {currentView === 'history' && (
              <ChatHistoryPanel 
                currentSessionId={currentSessionId}
                onSelectSession={(id) => {
                  onSelectSession(id)
                  setCurrentView('main')
                }}
              />
            )}

            {currentView === 'config' && (
              <div className="p-4 text-gray-400 flex-1 flex flex-col overflow-hidden">
                <button
                  onClick={() => setCurrentView('main')}
                  className="flex items-center gap-2 text-gray-300 hover:text-white transition-colors mb-4"
                >
                  <ChevronLeft className="w-5 h-5" />
                  <span>Back</span>
                </button>
                <h2 className="text-lg font-semibold text-white mb-4">⚙️ Session Config</h2>
                <div className="flex-1 bg-gray-800 rounded-lg p-4 overflow-auto">
                  <div className="space-y-3 text-sm">
                    <div>
                      <span className="text-gray-400">Model:</span>
                      <p className="text-white font-medium">GPT-4</p>
                    </div>
                    <div>
                      <span className="text-gray-400">Search Provider:</span>
                      <p className="text-white font-medium">SearXNG</p>
                    </div>
                    <div>
                      <span className="text-gray-400">Max Tokens:</span>
                      <p className="text-white font-medium">8192</p>
                    </div>
                    <div>
                      <span className="text-gray-400">Temperature:</span>
                      <p className="text-white font-medium">0.7</p>
                    </div>
                  </div>
                </div>
              </div>
            )}

            {currentView === 'messages' && (
              <div className="p-4 text-gray-400 flex-1 flex flex-col overflow-hidden">
                <button
                  onClick={() => setCurrentView('main')}
                  className="flex items-center gap-2 text-gray-300 hover:text-white transition-colors mb-4"
                >
                  <ChevronLeft className="w-5 h-5" />
                  <span>Back</span>
                </button>
                <h2 className="text-lg font-semibold text-white mb-4">📋 Session Messages</h2>
                <div className="flex-1 bg-gray-800 rounded-lg p-4 overflow-auto">
                  <div className="space-y-2 text-xs">
                    {apiMessages.map((msg, idx) => (
                      <div 
                        key={idx}
                        className={`p-2 rounded ${
                          msg.includes('📥') ? 'bg-blue-900 text-blue-200' :
                          msg.includes('✓') ? 'bg-green-900 text-green-200' :
                          msg.includes('Workflow') ? 'bg-purple-900 text-purple-200' :
                          msg.includes('Step') ? 'bg-orange-900 text-orange-200' :
                          'bg-gray-700 text-gray-200'
                        }`}
                      >
                        {msg}
                      </div>
                    ))}
                  </div>
                </div>
              </div>
            )}
          </>
        )}

        {/* COLLAPSED ICON BAR (vertical icons) */}
        {!sidebarOpen && sidebarTacked && (
          <>
            {/* Expand button at top */}
            <button
              onClick={() => setSidebarOpen?.(true)}
              className="p-3 hover:bg-gray-800 rounded transition-colors mx-1 my-2"
              title="Expand"
            >
              <PanelLeftOpen className="w-5 h-5" />
            </button>

            {/* Icon buttons */}
            <div className="flex-1 flex flex-col items-center gap-3 px-2 py-4">
              <button
                onClick={onNewChat}
                className="p-3 hover:bg-gray-800 rounded transition-colors"
                title="New Chat"
              >
                <Plus className="w-5 h-5 text-blue-400" />
              </button>
              
              <button
                onClick={() => setSidebarOpen?.(true)}
                className="p-3 hover:bg-gray-800 rounded transition-colors"
                title="Session Config"
              >
                <Settings className="w-5 h-5 text-gray-400" />
              </button>
              
              <button
                onClick={() => setSidebarOpen?.(true)}
                className="p-3 hover:bg-gray-800 rounded transition-colors"
                title="Messages"
              >
                <MessageSquare className="w-5 h-5 text-gray-400" />
              </button>

              <div className="border-t border-gray-700 w-6"></div>
              
              <button
                onClick={() => setSidebarOpen?.(true)}
                className="p-3 hover:bg-gray-800 rounded transition-colors"
                title="Chat History"
              >
                <History className="w-5 h-5 text-gray-400" />
              </button>
              
              <button
                onClick={() => setShowThemeDialog(true)}
                className="p-3 hover:bg-gray-800 rounded transition-colors"
                title="Themes"
              >
                <Palette className="w-5 h-5 text-gray-400" />
              </button>
            </div>
          </>
        )}

        {/* FLYOUT DRAWER (expanded & untacked) */}
        {sidebarOpen && !sidebarTacked && (
          <>
            {/* Header */}
            <div className="p-4 border-b border-gray-700 flex items-center justify-between">
              <h1 className="text-xl font-bold">Deep Research</h1>
              
              <div className="flex items-center gap-2">
                {/* Tack Icon (pin) - Untacked (outline only) */}
                <button
                  onClick={() => setSidebarTacked?.(true)}
                  className="p-1 hover:bg-gray-700 rounded transition-colors"
                  title="Dock sidebar (pin)"
                >
                  <Pin className="w-5 h-5 text-blue-400 stroke-2" />
                </button>

                {/* Collapse button */}
                <button
                  onClick={() => setSidebarOpen?.(false)}
                  className="p-1 hover:bg-gray-700 rounded transition-colors"
                  title="Collapse"
                >
                  <PanelLeftClose className="w-5 h-5" />
                </button>
              </div>
            </div>

            {/* Flyout content */}
            {currentView === 'main' && (
              <>
                <div className="p-4">
                  <button
                    onClick={onNewChat}
                    className="w-full flex items-center gap-2 px-4 py-3 bg-blue-600 hover:bg-blue-700 rounded-lg transition-colors"
                    title="New chat"
                  >
                    <Plus className="w-5 h-5" />
                    <span>New Chat</span>
                  </button>
                </div>

                <nav className="flex-1 px-2 space-y-1">
                  <SidebarItem 
                    icon={<Settings className="w-5 h-5" />} 
                    label="Session Config" 
                    onClick={() => setCurrentView('config')}
                  />
                  <SidebarItem 
                    icon={<MessageSquare className="w-5 h-5" />} 
                    label="Messages" 
                    onClick={() => setCurrentView('messages')}
                  />
                  <div className="border-t border-gray-700 my-2"></div>
                  <SidebarItem 
                    icon={<History className="w-5 h-5" />} 
                    label="Chat History" 
                    onClick={() => setCurrentView('history')}
                  />
                  <SidebarItem 
                    icon={<Palette className="w-5 h-5" />} 
                    label="Themes" 
                    onClick={() => setShowThemeDialog(true)}
                  />
                </nav>
              </>
            )}

            {currentView === 'history' && (
              <ChatHistoryPanel 
                currentSessionId={currentSessionId}
                onSelectSession={(id) => {
                  onSelectSession(id)
                  setCurrentView('main')
                  setSidebarTacked?.(true)
                }}
              />
            )}

            {currentView === 'config' && (
              <div className="p-4 text-gray-400 flex-1 flex flex-col overflow-hidden">
                <button
                  onClick={() => setCurrentView('main')}
                  className="flex items-center gap-2 text-gray-300 hover:text-white transition-colors mb-4"
                >
                  <ChevronLeft className="w-5 h-5" />
                  <span>Back</span>
                </button>
                <h2 className="text-lg font-semibold text-white mb-4">⚙️ Session Config</h2>
                <div className="flex-1 bg-gray-800 rounded-lg p-4 overflow-auto">
                  <div className="space-y-3 text-sm">
                    <div>
                      <span className="text-gray-400">Model:</span>
                      <p className="text-white font-medium">GPT-4</p>
                    </div>
                    <div>
                      <span className="text-gray-400">Search Provider:</span>
                      <p className="text-white font-medium">SearXNG</p>
                    </div>
                    <div>
                      <span className="text-gray-400">Max Tokens:</span>
                      <p className="text-white font-medium">8192</p>
                    </div>
                    <div>
                      <span className="text-gray-400">Temperature:</span>
                      <p className="text-white font-medium">0.7</p>
                    </div>
                  </div>
                </div>
              </div>
            )}

            {currentView === 'messages' && (
              <div className="p-4 text-gray-400 flex-1 flex flex-col overflow-hidden">
                <button
                  onClick={() => setCurrentView('main')}
                  className="flex items-center gap-2 text-gray-300 hover:text-white transition-colors mb-4"
                >
                  <ChevronLeft className="w-5 h-5" />
                  <span>Back</span>
                </button>
                <h2 className="text-lg font-semibold text-white mb-4">📋 Session Messages</h2>
                <div className="flex-1 bg-gray-800 rounded-lg p-4 overflow-auto">
                  <div className="space-y-2 text-xs">
                    {apiMessages.map((msg, idx) => (
                      <div 
                        key={idx}
                        className={`p-2 rounded ${
                          msg.includes('📥') ? 'bg-blue-900 text-blue-200' :
                          msg.includes('✓') ? 'bg-green-900 text-green-200' :
                          msg.includes('Workflow') ? 'bg-purple-900 text-purple-200' :
                          msg.includes('Step') ? 'bg-orange-900 text-orange-200' :
                          'bg-gray-700 text-gray-200'
                        }`}
                      >
                        {msg}
                      </div>
                    ))}
                  </div>
                </div>
              </div>
            )}
          </>
        )}

        {/* Collapse/Expand button at bottom (when icon bar) */}
        {!sidebarOpen && sidebarTacked && (
          <div className="border-t border-gray-700 px-1 py-2">
            {/* Already have expand at top */}
          </div>
        )}
      </aside>

      {/* Theme Dialog */}
      {showThemeDialog && (
        <ThemeDialog onClose={() => setShowThemeDialog(false)} />
      )}
    </>
  )
}

function SidebarItem({ icon, label, onClick, title }: { icon: React.ReactNode; label: string; onClick?: () => void, title?: string }) {
  return (
    <button 
      onClick={onClick}
      title={title}
      className="w-full flex items-center gap-3 px-3 py-2 hover:bg-gray-800 rounded-lg transition-colors text-left group"
    >
      <span className="text-gray-400 group-hover:text-gray-200">{icon}</span>
      <span className="text-sm">{label}</span>
    </button>
  )
}
