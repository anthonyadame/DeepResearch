import React, { useMemo, useEffect, useState } from 'react'
import { X, Trash2, RotateCcw } from 'lucide-react'
import { useDebugStore } from '@stores/debugStore'
import DebugMessageItem from './DebugMessageItem'
import type { APILog } from '@stores/apiLogStore'
import { apiLogStore } from '@stores/apiLogStore'

interface DebugConsoleProps {
  isVisible: boolean
  height: number
}

export default function DebugConsole({ isVisible, height }: DebugConsoleProps) {
  const { messages, activeTab, setActiveTab, clearMessages } = useDebugStore()
  const [apiLogs, setApiLogs] = useState<APILog[]>([])

  useEffect(() => {
    // Subscribe to API logs
    const unsubscribe = apiLogStore.onChange((newLogs) => {
      setApiLogs(newLogs)
    })
    return unsubscribe
  }, [])

  const filteredMessages = useMemo(() => {
    switch (activeTab) {
      case 'messages':
        return messages.filter((m) => m.type === 'message')
      case 'state':
        return messages.filter((m) => m.type === 'state')
      case 'api_calls':
        return messages.filter((m) => m.type === 'api_call' || m.type === 'error')
      case 'api_logs':
        return apiLogs
      default:
        return messages
    }
  }, [messages, activeTab, apiLogs])

  if (!isVisible) return null

  return (
    <div
      className="flex flex-col bg-gray-900 text-gray-100 border-t border-gray-700 font-mono text-xs overflow-hidden"
      style={{ height: `${height}%` }}
    >
      {/* Header */}
      <div className="flex items-center justify-between px-4 py-2 bg-gray-800 border-b border-gray-700 flex-shrink-0">
        <div className="flex items-center gap-4">
          <h3 className="font-semibold">🖥️ Debug Console</h3>
          <div className="flex items-center gap-1 bg-gray-700 rounded-md p-0.5">
            <TabButton
              active={activeTab === 'messages'}
              onClick={() => setActiveTab('messages')}
              label="Messages"
              count={messages.filter((m) => m.type === 'message').length}
            />
            <TabButton
              active={activeTab === 'state'}
              onClick={() => setActiveTab('state')}
              label="State"
              count={messages.filter((m) => m.type === 'state').length}
            />
            <TabButton
              active={activeTab === 'api_calls'}
              onClick={() => setActiveTab('api_calls')}
              label="API"
              count={messages.filter((m) => m.type === 'api_call' || m.type === 'error').length}
            />
            <TabButton
              active={activeTab === 'api_logs'}
              onClick={() => setActiveTab('api_logs')}
              label="🔄 Logs"
              count={apiLogs.length}
            />
          </div>
        </div>

        {/* Actions */}
        <div className="flex gap-2">
          <button
            onClick={() => {
              clearMessages()
              apiLogStore.clear()
            }}
            className="p-1.5 hover:bg-gray-700 rounded transition-colors"
            title="Clear all"
          >
            <RotateCcw className="w-4 h-4" />
          </button>
        </div>
      </div>

      {/* Content */}
      <div className="flex-1 overflow-y-auto bg-gray-950 p-4 space-y-1">
        {activeTab === 'api_logs' ? (
          // API Logs Display
          apiLogs.length === 0 ? (
            <div className="text-gray-600">No API logs yet...</div>
          ) : (
            apiLogs.map((log, index) => (
              <div
                key={index}
                className={`flex gap-2 py-0.5 ${
                  log.level === 'error'
                    ? 'text-red-400'
                    : log.level === 'warning'
                    ? 'text-yellow-400'
                    : log.level === 'debug'
                    ? 'text-gray-500'
                    : 'text-green-400'
                }`}
              >
                <span className="text-gray-600 flex-shrink-0 w-12">{log.timestamp}</span>
                <span className="flex-shrink-0 w-4">{log.icon}</span>
                <span className="flex-shrink-0 w-20 text-gray-500">[{log.category}]</span>
                <span className="flex-1 break-words">{log.message}</span>
              </div>
            ))
          )
        ) : filteredMessages.length === 0 ? (
          <div className="text-gray-600">No messages...</div>
        ) : (
          filteredMessages.map((msg, index) => (
            <DebugMessageItem key={index} message={msg} />
          ))
        )}
      </div>

      {/* Footer */}
      <div className="px-4 py-2 bg-gray-800 border-t border-gray-700 text-xs text-gray-500 flex-shrink-0">
        Showing {filteredMessages.length} items
      </div>
    </div>
  )
}

interface TabButtonProps {
  active: boolean
  onClick: () => void
  label: string
  count: number
}

function TabButton({ active, onClick, label, count }: TabButtonProps) {
  return (
    <button
      onClick={onClick}
      className={`px-2 py-1 rounded text-xs font-medium transition-colors ${
        active
          ? 'bg-blue-600 text-white'
          : 'text-gray-300 hover:bg-gray-600'
      }`}
    >
      {label} ({count})
    </button>
  )
}
