import { Settings, Info, PanelRightOpen, PanelRightClose, Pin } from 'lucide-react'

interface RightSidebarProps {
  sessionId: string | null
  rightSidebarOpen?: boolean
  setRightSidebarOpen?: (open: boolean) => void
  rightSidebarTacked?: boolean
  setRightSidebarTacked?: (tacked: boolean) => void
  rightSidebarView?: 'config' | 'info'
  setRightSidebarView?: (view: 'config' | 'info') => void
}

export default function RightSidebar({
  sessionId,
  rightSidebarOpen = false,
  setRightSidebarOpen,
  rightSidebarTacked = true,
  setRightSidebarTacked,
  rightSidebarView = 'info',
  setRightSidebarView
}: RightSidebarProps) {
  return (
    <>
      {/* Flyout overlay (only when untacked and expanded) */}
      {!rightSidebarTacked && rightSidebarOpen && (
        <div
          className="fixed inset-0 bg-black bg-opacity-50 z-20"
          onClick={() => setRightSidebarTacked?.(true)}
        />
      )}

      {/* Right Sidebar - Part of main flex layout */}
      <aside
        className={`transition-all duration-300 h-screen bg-gray-900 text-white z-40 flex flex-col flex-shrink-0 ${
          // Flyout (expanded & untacked) - fixed overlay
          rightSidebarOpen && !rightSidebarTacked
            ? 'fixed right-0 top-0 w-64 shadow-lg'
            // Full sidebar (expanded & tacked)
            : rightSidebarOpen && rightSidebarTacked
            ? 'relative w-64 border-l border-gray-700'
            // Icon bar (collapsed & tacked)
            : !rightSidebarOpen && rightSidebarTacked
            ? 'relative w-16 border-l border-gray-700 items-center'
            // Icon bar (collapsed & untacked)
            : 'relative w-16 border-l border-gray-700 items-center'
        }`}
      >
        {/* Full Sidebar (Expanded & Tacked) */}
        {rightSidebarOpen && rightSidebarTacked && (
          <>
            <div className="p-4 border-b border-gray-700 flex items-center justify-between">
              <h1 className="text-xl font-bold">
                {rightSidebarView === 'config' ? '⚙️ Config' : 'ℹ️ Info'}
              </h1>
              
              <div className="flex items-center gap-2">
                <button
                  onClick={() => setRightSidebarTacked?.(false)}
                  className="p-1 hover:bg-gray-700 rounded transition-colors"
                  title="Overlay mode"
                >
                  <Pin className="w-5 h-5 fill-blue-400 text-blue-400" />
                </button>
                
                <button
                  onClick={() => setRightSidebarOpen?.(false)}
                  className="p-1 hover:bg-gray-700 rounded transition-colors"
                  title="Collapse"
                >
                  <PanelRightClose className="w-5 h-5" />
                </button>
              </div>
            </div>

            <div className="flex gap-2 p-4 border-b border-gray-700">
              <button
                onClick={() => setRightSidebarView?.('config')}
                className={`px-3 py-1.5 rounded text-sm font-medium transition-colors ${
                  rightSidebarView === 'config'
                    ? 'bg-blue-600 text-white'
                    : 'bg-gray-800 text-gray-400 hover:text-white'
                }`}
              >
                ⚙️ Config
              </button>
              <button
                onClick={() => setRightSidebarView?.('info')}
                className={`px-3 py-1.5 rounded text-sm font-medium transition-colors ${
                  rightSidebarView === 'info'
                    ? 'bg-blue-600 text-white'
                    : 'bg-gray-800 text-gray-400 hover:text-white'
                }`}
              >
                ℹ️ Info
              </button>
            </div>

            <div className="flex-1 overflow-auto p-4 space-y-3 text-sm">
              {rightSidebarView === 'config' && (
                <>
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
                </>
              )}
              {rightSidebarView === 'info' && (
                <>
                  <div>
                    <span className="text-gray-400">Session ID:</span>
                    <p className="font-mono text-xs break-all text-white">{sessionId}</p>
                  </div>
                  <div>
                    <span className="text-gray-400">Status:</span>
                    <p className="text-white">📍 Connected</p>
                  </div>
                  <div>
                    <span className="text-gray-400">Created:</span>
                    <p className="text-white">{new Date().toLocaleDateString()}</p>
                  </div>
                </>
              )}
            </div>
          </>
        )}

        {/* Icon Bar (Collapsed & Tacked) */}
        {!rightSidebarOpen && rightSidebarTacked && (
          <>
            <button
              onClick={() => setRightSidebarOpen?.(true)}
              className="p-3 hover:bg-gray-800 rounded transition-colors mx-1 my-2"
              title="Expand"
            >
              <PanelRightOpen className="w-5 h-5" />
            </button>

            <div className="flex-1 flex flex-col items-center gap-3 px-2 py-4">
              <button
                onClick={() => {
                  setRightSidebarOpen?.(true)
                  setRightSidebarView?.('config')
                }}
                className="p-3 hover:bg-gray-800 rounded transition-colors"
                title="Session Config"
              >
                <Settings className="w-5 h-5 text-gray-400" />
              </button>
              
              <button
                onClick={() => {
                  setRightSidebarOpen?.(true)
                  setRightSidebarView?.('info')
                }}
                className="p-3 hover:bg-gray-800 rounded transition-colors"
                title="Session Info"
              >
                <Info className="w-5 h-5 text-gray-400" />
              </button>
            </div>
          </>
        )}

        {/* Flyout (Expanded & Untacked) */}
        {rightSidebarOpen && !rightSidebarTacked && (
          <>
            <div className="p-4 border-b border-gray-700 flex items-center justify-between">
              <h1 className="text-xl font-bold">
                {rightSidebarView === 'config' ? '⚙️ Config' : 'ℹ️ Info'}
              </h1>
              
              <div className="flex items-center gap-2">
                <button
                  onClick={() => setRightSidebarTacked?.(true)}
                  className="p-1 hover:bg-gray-700 rounded transition-colors"
                  title="Dock sidebar"
                >
                  <Pin className="w-5 h-5 text-blue-400 stroke-2" />
                </button>

                <button
                  onClick={() => setRightSidebarOpen?.(false)}
                  className="p-1 hover:bg-gray-700 rounded transition-colors"
                  title="Collapse"
                >
                  <PanelRightClose className="w-5 h-5" />
                </button>
              </div>
            </div>

            <div className="flex gap-2 p-4 border-b border-gray-700">
              <button
                onClick={() => setRightSidebarView?.('config')}
                className={`px-3 py-1.5 rounded text-sm font-medium transition-colors ${
                  rightSidebarView === 'config'
                    ? 'bg-blue-600 text-white'
                    : 'bg-gray-800 text-gray-400 hover:text-white'
                }`}
              >
                ⚙️ Config
              </button>
              <button
                onClick={() => setRightSidebarView?.('info')}
                className={`px-3 py-1.5 rounded text-sm font-medium transition-colors ${
                  rightSidebarView === 'info'
                    ? 'bg-blue-600 text-white'
                    : 'bg-gray-800 text-gray-400 hover:text-white'
                }`}
              >
                ℹ️ Info
              </button>
            </div>

            <div className="flex-1 overflow-auto p-4 space-y-3 text-sm">
              {rightSidebarView === 'config' && (
                <>
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
                </>
              )}
              {rightSidebarView === 'info' && (
                <>
                  <div>
                    <span className="text-gray-400">Session ID:</span>
                    <p className="font-mono text-xs break-all text-white">{sessionId}</p>
                  </div>
                  <div>
                    <span className="text-gray-400">Status:</span>
                    <p className="text-white">📍 Connected</p>
                  </div>
                  <div>
                    <span className="text-gray-400">Created:</span>
                    <p className="text-white">{new Date().toLocaleDateString()}</p>
                  </div>
                </>
              )}
            </div>
          </>
        )}
      </aside>
    </>
  )
}
