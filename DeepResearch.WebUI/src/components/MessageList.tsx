import React, { useEffect, useRef, useState } from 'react'
import { ChevronDown } from 'lucide-react'
import type { ChatMessage } from '@types/index'
import MessageBubble from './MessageBubble'

interface MessageListProps {
  messages: ChatMessage[]
  isLoading?: boolean
}

export default function MessageList({ messages, isLoading }: MessageListProps) {
  const messagesEndRef = useRef<HTMLDivElement>(null)
  const containerRef = useRef<HTMLDivElement>(null)
  const [showScrollButton, setShowScrollButton] = useState(false)

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }

  const handleScroll = () => {
    if (!containerRef.current) return
    
    const { scrollTop, scrollHeight, clientHeight } = containerRef.current
    const isNearBottom = scrollHeight - scrollTop - clientHeight < 100
    setShowScrollButton(!isNearBottom)
  }

  useEffect(() => {
    // Auto-scroll when new messages arrive
    scrollToBottom()
  }, [messages.length])

  return (
    <div className="relative flex-1 overflow-hidden">
      <div 
        ref={containerRef}
        className="h-full overflow-y-auto px-4 py-4 scroll-smooth"
        onScroll={handleScroll}
      >
        {messages.length === 0 ? (
          <div className="flex items-center justify-center h-full">
            <div className="text-center space-y-3">
              <div className="w-16 h-16 mx-auto bg-gradient-to-br from-blue-100 to-purple-100 rounded-full flex items-center justify-center">
                <span className="text-3xl">🔍</span>
              </div>
              <p className="text-gray-500 font-medium">Start your research</p>
              <p className="text-sm text-gray-400">Ask me anything and I'll help you find answers</p>
            </div>
          </div>
        ) : (
          <div className="max-w-4xl mx-auto space-y-2">
            {messages.map((msg) => (
              <MessageBubble key={msg.id || crypto.randomUUID()} message={msg} />
            ))}
            {isLoading && (
              <div className="flex items-start gap-3 mb-4">
                <div className="flex-shrink-0 w-8 h-8 rounded-full bg-gradient-to-br from-gray-700 to-gray-900 flex items-center justify-center">
                  <span className="text-white text-sm">🤖</span>
                </div>
                <div className="bg-white border border-gray-200 rounded-2xl rounded-tl-sm px-4 py-3 shadow-sm">
                  <div className="flex items-center gap-2">
                    <div className="flex gap-1">
                      <div className="w-2 h-2 bg-blue-400 rounded-full animate-bounce" />
                      <div className="w-2 h-2 bg-blue-400 rounded-full animate-bounce" style={{ animationDelay: '0.1s' }} />
                      <div className="w-2 h-2 bg-blue-400 rounded-full animate-bounce" style={{ animationDelay: '0.2s' }} />
                    </div>
                    <span className="text-sm text-gray-600">Researching...</span>
                  </div>
                </div>
              </div>
            )}
            <div ref={messagesEndRef} />
          </div>
        )}
      </div>

      {/* Scroll to Bottom Button */}
      {showScrollButton && (
        <button
          onClick={scrollToBottom}
          className="absolute bottom-4 right-4 bg-white border border-gray-200 rounded-full p-2 shadow-lg hover:shadow-xl transition-all hover:scale-105"
          title="Scroll to bottom"
        >
          <ChevronDown className="w-5 h-5 text-gray-600" />
        </button>
      )}
    </div>
  )
}
