import React, { useState, useRef, useEffect } from 'react'
import { Send, Maximize2 } from 'lucide-react'

interface InputBarProps {
  value: string
  onChange: (value: string) => void
  onSend: () => void
  isLoading?: boolean
  onOpenExpanded?: () => void
}

const MIN_HEIGHT = 44 // ~2 rows
const MAX_HEIGHT = 200 // ~8-10 rows

export default function InputBar({ value, onChange, onSend, isLoading, onOpenExpanded }: InputBarProps) {
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const [height, setHeight] = useState(MIN_HEIGHT)

  useEffect(() => {
    if (textareaRef.current) {
      // Reset height to auto to get the correct scrollHeight
      textareaRef.current.style.height = 'auto'
      const scrollHeight = textareaRef.current.scrollHeight
      const newHeight = Math.min(Math.max(scrollHeight, MIN_HEIGHT), MAX_HEIGHT)
      setHeight(newHeight)
      textareaRef.current.style.height = `${newHeight}px`
    }
  }, [value])

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      onSend()
    }
  }

  return (
    <div className="flex gap-2 items-end">
      <div className="flex-1 relative">
        <textarea
          ref={textareaRef}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="Type your research question..."
          className="w-full p-3 pr-10 border border-gray-300 dark:border-gray-600 rounded-lg resize-none focus:outline-none focus:ring-2 focus:ring-blue-500 bg-white dark:bg-gray-800 text-gray-900 dark:text-white placeholder-gray-400 transition-all"
          style={{ height: `${height}px`, overflow: height >= MAX_HEIGHT ? 'auto' : 'hidden' }}
          disabled={isLoading}
        />
        {onOpenExpanded && (
          <button
            onClick={onOpenExpanded}
            className="absolute right-2 bottom-2 p-1.5 hover:bg-gray-100 dark:hover:bg-gray-700 rounded transition-colors"
            title="Expand input"
          >
            <Maximize2 className="w-4 h-4 text-gray-500 dark:text-gray-400" />
          </button>
        )}
      </div>
      <button
        onClick={onSend}
        disabled={isLoading || !value.trim()}
        className="p-3 bg-blue-500 hover:bg-blue-600 disabled:bg-gray-300 dark:disabled:bg-gray-600 text-white rounded-lg transition-colors flex-shrink-0"
        title="Send message (Enter)"
      >
        <Send className="w-5 h-5" />
      </button>
    </div>
  )
}
