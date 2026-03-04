import { useState, useEffect, useRef } from 'react'
import { X, Send, Type } from 'lucide-react'

interface ExpandedInputDialogProps {
  value: string
  onChange: (value: string) => void
  onSend: () => void
  onClose: () => void
  isLoading?: boolean
}

export default function ExpandedInputDialog({
  value,
  onChange,
  onSend,
  onClose,
  isLoading,
}: ExpandedInputDialogProps) {
  const [localValue, setLocalValue] = useState(value)
  const [width, setWidth] = useState(1024) // Wider default
  const [height, setHeight] = useState(typeof window !== 'undefined' ? window.innerHeight * 0.666 : 720) // 2/3 of viewport height
  const [isDragging, setIsDragging] = useState(false)
  const [dragType, setDragType] = useState<string>('')
  const [startX, setStartX] = useState(0)
  const [startY, setStartY] = useState(0)
  const [startWidth, setStartWidth] = useState(0)
  const [startHeight, setStartHeight] = useState(0)
  const textareaRef = useRef<HTMLTextAreaElement>(null)
  const dialogRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    setLocalValue(value)
  }, [value])

  useEffect(() => {
    // Focus textarea when dialog opens
    if (textareaRef.current) {
      textareaRef.current.focus()
      textareaRef.current.setSelectionRange(localValue.length, localValue.length)
    }
  }, [])

  const handleSend = () => {
    onChange(localValue)
    onSend()
    onClose()
  }

  const handleCancel = () => {
    setLocalValue(value)
    onClose()
  }

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
      e.preventDefault()
      handleSend()
    }
    if (e.key === 'Escape') {
      e.preventDefault()
      handleCancel()
    }
  }

  const handleResizeStart = (type: string, e: React.MouseEvent) => {
    e.preventDefault()
    setIsDragging(true)
    setDragType(type)
    setStartX(e.clientX)
    setStartY(e.clientY)
    setStartWidth(width)
    setStartHeight(height)
  }

  useEffect(() => {
    const handleMouseMove = (e: MouseEvent) => {
      if (!isDragging) return

      const deltaX = e.clientX - startX
      const deltaY = e.clientY - startY

      if (dragType === 'right') {
        setWidth(Math.max(400, Math.min(1200, startWidth + deltaX)))
      } else if (dragType === 'bottom') {
        const maxHeight = typeof window !== 'undefined' ? window.innerHeight * 0.85 : 900
        const minHeight = typeof window !== 'undefined' ? window.innerHeight * 0.5 : 300
        setHeight(Math.max(minHeight, Math.min(maxHeight, startHeight + deltaY)))
      } else if (dragType === 'corner') {
        setWidth(Math.max(400, Math.min(1200, startWidth + deltaX)))
        const maxHeight = typeof window !== 'undefined' ? window.innerHeight * 0.85 : 900
        const minHeight = typeof window !== 'undefined' ? window.innerHeight * 0.5 : 300
        setHeight(Math.max(minHeight, Math.min(maxHeight, startHeight + deltaY)))
      }
    }

    const handleMouseUp = () => {
      setIsDragging(false)
      setDragType('')
    }

    if (isDragging) {
      document.addEventListener('mousemove', handleMouseMove)
      document.addEventListener('mouseup', handleMouseUp)
      return () => {
        document.removeEventListener('mousemove', handleMouseMove)
        document.removeEventListener('mouseup', handleMouseUp)
      }
    }
  }, [isDragging, dragType, startX, startY, startWidth, startHeight])

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-start justify-center z-50 p-4 pt-8">
      <div
        ref={dialogRef}
        className="bg-white dark:bg-gray-900 rounded-2xl shadow-2xl flex flex-col border border-gray-200 dark:border-gray-700 relative"
        style={{
          width: `${width}px`,
          height: `${height}px`,
          userSelect: isDragging ? 'none' : 'auto',
          maxHeight: 'calc(100vh - 2rem)',
        }}
      >
        {/* Header */}
        <div className="flex items-center justify-between p-4 border-b border-gray-200 dark:border-gray-700 flex-shrink-0">
          <div className="flex items-center gap-3">
            <Type className="w-5 h-5 text-blue-600" />
            <h2 className="text-lg font-semibold text-gray-900 dark:text-white">Expanded Input</h2>
          </div>
          <button
            onClick={handleCancel}
            className="p-1 hover:bg-gray-100 dark:hover:bg-gray-800 rounded transition-colors"
          >
            <X className="w-5 h-5 text-gray-500" />
          </button>
        </div>

        {/* Content */}
        <div className="flex-1 flex flex-col p-4 gap-3 overflow-hidden">
          <textarea
            ref={textareaRef}
            value={localValue}
            onChange={(e) => {
              setLocalValue(e.target.value)
              onChange(e.target.value)
            }}
            onKeyDown={handleKeyDown}
            placeholder="Type your research question here... (Ctrl+Enter to send, Escape to close)"
            className="flex-1 p-4 border border-gray-300 dark:border-gray-600 rounded-lg resize-none focus:outline-none focus:ring-2 focus:ring-blue-500 bg-white dark:bg-gray-800 text-gray-900 dark:text-white placeholder-gray-400 transition-all"
            disabled={isLoading}
          />
          <p className="text-xs text-gray-500 dark:text-gray-400">
            {localValue.length} characters • {localValue.trim().split(/\s+/).filter(Boolean).length} words
          </p>
        </div>

        {/* Footer */}
        <div className="flex items-center justify-between p-4 border-t border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800 flex-shrink-0">
          <p className="text-xs text-gray-500 dark:text-gray-400">
            Drag edges to resize • Ctrl+Enter to send
          </p>
          <div className="flex gap-3">
            <button
              onClick={handleCancel}
              className="px-4 py-2 text-gray-700 dark:text-gray-300 border border-gray-300 dark:border-gray-600 rounded-lg hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
            >
              Close
            </button>
            <button
              onClick={handleSend}
              disabled={isLoading || !localValue.trim()}
              className="px-4 py-2 bg-blue-600 hover:bg-blue-700 disabled:bg-gray-300 dark:disabled:bg-gray-600 text-white rounded-lg transition-colors flex items-center gap-2"
            >
              <Send className="w-4 h-4" />
              Send
            </button>
          </div>
        </div>

        {/* Resize Handles */}
        {/* Right edge */}
        <div
          onMouseDown={(e) => handleResizeStart('right', e)}
          className="absolute right-0 top-0 bottom-0 w-1 cursor-col-resize hover:bg-blue-500 hover:w-1.5 transition-all"
          title="Drag to resize width"
        />
        {/* Bottom edge */}
        <div
          onMouseDown={(e) => handleResizeStart('bottom', e)}
          className="absolute bottom-0 left-0 right-0 h-1 cursor-row-resize hover:bg-blue-500 hover:h-1.5 transition-all"
          title="Drag to resize height"
        />
        {/* Corner (bottom-right) */}
        <div
          onMouseDown={(e) => handleResizeStart('corner', e)}
          className="absolute bottom-0 right-0 w-4 h-4 cursor-nwse-resize"
          title="Drag corner to resize both"
        >
          <svg className="w-4 h-4 text-gray-400 dark:text-gray-600" fill="currentColor" viewBox="0 0 16 16">
            <path d="M16 16v-2h-2v2h2zm-4 0v-2h-2v2h2zm-4 0v-2H6v2h2zm-4 0v-2H2v2h2z" />
          </svg>
        </div>
      </div>
    </div>
  )
}
