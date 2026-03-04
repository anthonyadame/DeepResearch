import React from 'react'
import { User, Bot } from 'lucide-react'
import type { ChatMessage } from '@types/index'

interface MessageBubbleProps {
  message: ChatMessage
}

export default function MessageBubble({ message }: MessageBubbleProps) {
  const isUser = message.role === 'user'
  const isSystem = message.role === 'system'

  // Format timestamp
  const formatTime = (timestamp?: string) => {
    if (!timestamp) return ''
    const date = new Date(timestamp)
    const now = new Date()
    const diffInSeconds = Math.floor((now.getTime() - date.getTime()) / 1000)

    if (diffInSeconds < 60) return 'Just now'
    if (diffInSeconds < 3600) return `${Math.floor(diffInSeconds / 60)}m ago`
    if (diffInSeconds < 86400) return `${Math.floor(diffInSeconds / 3600)}h ago`
    return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
  }

  if (isSystem) {
    return (
      <div className="flex justify-center my-2">
        <div className="px-3 py-1 bg-gray-100 text-gray-600 text-xs rounded-full">
          {message.content}
        </div>
      </div>
    )
  }

  return (
    <div
      className={`flex gap-3 mb-4 animate-fadeIn ${isUser ? 'flex-row-reverse' : 'flex-row'}`}
      style={{ animation: 'fadeIn 0.3s ease-in' }}
    >
      {/* Avatar */}
      <div
        className={`flex-shrink-0 w-8 h-8 rounded-full flex items-center justify-center ${
          isUser
            ? 'bg-gradient-to-br from-blue-500 to-purple-600'
            : 'bg-gradient-to-br from-gray-700 to-gray-900'
        }`}
      >
        {isUser ? (
          <User className="w-5 h-5 text-white" />
        ) : (
          <Bot className="w-5 h-5 text-white" />
        )}
      </div>

      {/* Message Content */}
      <div
        className={`flex flex-col max-w-[70%] ${isUser ? 'items-end' : 'items-start'}`}
      >
        <div
          className={`px-4 py-3 rounded-2xl shadow-sm ${
            isUser
              ? 'bg-gradient-to-br from-blue-500 to-blue-600 text-white rounded-tr-sm'
              : 'bg-white border border-gray-200 text-gray-800 rounded-tl-sm'
          }`}
        >
          <p className="text-sm leading-relaxed whitespace-pre-wrap break-words">
            {message.content}
          </p>
        </div>

        {/* Timestamp */}
        <span
          className={`text-xs text-gray-400 mt-1 px-1 ${
            isUser ? 'text-right' : 'text-left'
          }`}
        >
          {formatTime(message.timestamp)}
        </span>
      </div>
    </div>
  )
}
