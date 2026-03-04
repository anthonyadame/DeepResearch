import React from 'react'
import { GripHorizontal } from 'lucide-react'

interface ResizableDividerProps {
  onMouseDown: () => void
  isDragging: boolean
}

export default function ResizableDivider({ onMouseDown, isDragging }: ResizableDividerProps) {
  return (
    <div
      className={`flex items-center justify-center h-1 bg-gray-200 cursor-ns-resize hover:bg-blue-400 transition-colors group relative ${
        isDragging ? 'bg-blue-500' : ''
      }`}
      onMouseDown={onMouseDown}
    >
      <div className="absolute inset-0 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity">
        <GripHorizontal className="w-6 h-6 text-gray-500" />
      </div>
    </div>
  )
}
