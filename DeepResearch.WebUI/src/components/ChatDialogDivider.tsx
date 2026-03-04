import React from 'react'
import { GripVertical } from 'lucide-react'

interface ChatDialogDividerProps {
  onMouseDown: (e: React.MouseEvent) => void
  isDragging: boolean
}

export default function ChatDialogDivider({ onMouseDown, isDragging }: ChatDialogDividerProps) {
  return (
    <div
      onMouseDown={onMouseDown}
      className={`w-1 cursor-col-resize transition-all group hover:w-1.5 ${
        isDragging
          ? 'w-1.5 bg-blue-500'
          : 'bg-gray-200 hover:bg-gray-300 dark:bg-gray-700 dark:hover:bg-gray-600'
      }`}
      title="Drag to resize chat panel"
    >
      <div className="h-full flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity">
        <GripVertical className="w-3 h-3 text-gray-400" />
      </div>
    </div>
  )
}
