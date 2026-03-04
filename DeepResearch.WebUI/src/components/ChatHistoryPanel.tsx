import { useState } from 'react'
import { MessageSquare, Trash2, Search as SearchIcon } from 'lucide-react'
import { useChatHistory } from '@hooks/useChatHistory'

interface ChatHistoryPanelProps {
  currentSessionId: string | null
  onSelectSession: (sessionId: string) => void
}

export default function ChatHistoryPanel({ currentSessionId, onSelectSession }: ChatHistoryPanelProps) {
  const { sessions, isLoading, deleteSession } = useChatHistory()
  const [searchQuery, setSearchQuery] = useState('')

  const filteredSessions = sessions.filter(session =>
    session.title.toLowerCase().includes(searchQuery.toLowerCase())
  )

  const handleDelete = async (sessionId: string, e: React.MouseEvent) => {
    e.stopPropagation()
    if (confirm('Delete this chat session?')) {
      try {
        await deleteSession(sessionId)
      } catch (err) {
        console.error('Failed to delete session:', err)
      }
    }
  }

  return (
    <div className="flex flex-col h-full">
      {/* Search */}
      <div className="p-4 border-b border-gray-700">
        <div className="relative">
          <SearchIcon className="absolute left-3 top-1/2 transform -translate-y-1/2 w-4 h-4 text-gray-400" />
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder="Search chats..."
            className="w-full pl-10 pr-4 py-2 bg-gray-800 border border-gray-700 rounded-lg text-sm text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </div>
      </div>

      {/* Sessions List */}
      <div className="flex-1 overflow-y-auto">
        {isLoading ? (
          <div className="p-4 text-center text-gray-400">Loading...</div>
        ) : filteredSessions.length === 0 ? (
          <div className="p-4 text-center text-gray-400">
            {searchQuery ? 'No matching chats' : 'No chat history'}
          </div>
        ) : (
          <div className="p-2 space-y-1">
            {filteredSessions.map((session) => (
              <button
                key={session.id}
                onClick={() => onSelectSession(session.id)}
                className={`w-full p-3 rounded-lg transition-colors text-left group ${
                  currentSessionId === session.id
                    ? 'bg-gray-800 text-white'
                    : 'hover:bg-gray-800 text-gray-300'
                }`}
              >
                <div className="flex items-start justify-between gap-2">
                  <div className="flex items-start gap-2 flex-1 min-w-0">
                    <MessageSquare className="w-4 h-4 mt-1 flex-shrink-0" />
                    <div className="flex-1 min-w-0">
                      <div className="font-medium truncate">{session.title}</div>
                      <div className="text-xs text-gray-500 mt-1">
                        {new Date(session.updatedAt).toLocaleDateString()}
                      </div>
                    </div>
                  </div>
                  <button
                    onClick={(e) => handleDelete(session.id, e)}
                    className="opacity-0 group-hover:opacity-100 p-1 hover:bg-gray-700 rounded transition-all"
                    title="Delete chat"
                  >
                    <Trash2 className="w-4 h-4 text-red-400" />
                  </button>
                </div>
              </button>
            ))}
          </div>
        )}
      </div>
    </div>
  )
}
