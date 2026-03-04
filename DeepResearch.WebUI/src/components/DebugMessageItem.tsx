import React, { useState } from 'react'
import { Copy, Check, ChevronDown, ChevronRight } from 'lucide-react'
import type { DebugMessage } from '@stores/debugStore'

interface DebugMessageItemProps {
  message: DebugMessage
}

export default function DebugMessageItem({ message }: DebugMessageItemProps) {
  const [isCopied, setIsCopied] = useState(false)
  const [isExpanded, setIsExpanded] = useState(false)

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(JSON.stringify(message.data, null, 2))
      setIsCopied(true)
      setTimeout(() => setIsCopied(false), 2000)
    } catch (err) {
      console.error('Failed to copy:', err)
    }
  }

  const getTypeBadge = () => {
    const badges = {
      message: 'bg-blue-100 text-blue-700',
      state: 'bg-purple-100 text-purple-700',
      error: 'bg-red-100 text-red-700',
      api_call: 'bg-green-100 text-green-700',
    }
    return badges[message.type] || 'bg-gray-100 text-gray-700'
  }

  const getDirectionIcon = () => {
    return message.direction === 'sent' ? '↑' : '↓'
  }

  return (
    <div className="border-b border-gray-200 last:border-b-0">
      <div
        className="flex items-center gap-2 px-3 py-2 hover:bg-gray-50 cursor-pointer"
        onClick={() => setIsExpanded(!isExpanded)}
      >
        {/* Expand/Collapse Icon */}
        <button className="flex-shrink-0">
          {isExpanded ? (
            <ChevronDown className="w-4 h-4 text-gray-500" />
          ) : (
            <ChevronRight className="w-4 h-4 text-gray-500" />
          )}
        </button>

        {/* Direction Icon */}
        <span className="text-sm font-mono">{getDirectionIcon()}</span>

        {/* Type Badge */}
        <span className={`px-2 py-0.5 rounded text-xs font-medium ${getTypeBadge()}`}>
          {message.type}
        </span>

        {/* Timestamp */}
        <span className="text-xs text-gray-500">
          {message.timestamp.toLocaleTimeString()}
        </span>

        {/* Endpoint (if API call) */}
        {message.endpoint && (
          <span className="text-xs text-gray-600 font-mono truncate flex-1">
            {message.endpoint}
          </span>
        )}

        {/* Status Code (if API response) */}
        {message.statusCode && (
          <span
            className={`px-2 py-0.5 rounded text-xs font-mono ${
              message.statusCode >= 200 && message.statusCode < 300
                ? 'bg-green-100 text-green-700'
                : 'bg-red-100 text-red-700'
            }`}
          >
            {message.statusCode}
          </span>
        )}

        {/* Copy Button */}
        <button
          onClick={(e) => {
            e.stopPropagation()
            handleCopy()
          }}
          className="ml-auto flex-shrink-0 p-1 hover:bg-gray-200 rounded transition-colors"
          title="Copy to clipboard"
        >
          {isCopied ? (
            <Check className="w-4 h-4 text-green-600" />
          ) : (
            <Copy className="w-4 h-4 text-gray-600" />
          )}
        </button>
      </div>

      {/* Expanded Content */}
      {isExpanded && (
        <div className="px-3 py-2 bg-gray-900 text-gray-100 overflow-x-auto">
          <pre className="text-xs font-mono whitespace-pre-wrap">
            {JSON.stringify(message.data, null, 2)}
          </pre>
        </div>
      )}
    </div>
  )
}
