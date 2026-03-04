import { useState, useCallback } from 'react'

interface UseResizableOptions {
  initialWidth?: number
  minWidth?: number
  maxWidth?: number
  onResize?: (width: number) => void
}

export const useChatDialogWidth = (options: UseResizableOptions = {}) => {
  const {
    initialWidth = 66, // 2/3 of page (in percent)
    minWidth = 40,     // Minimum 40% width
    maxWidth = 80,     // Maximum 80% width
    onResize
  } = options

  const [width, setWidth] = useState(initialWidth)
  const [isDragging, setIsDragging] = useState(false)

  const startResizing = useCallback(() => {
    setIsDragging(true)
  }, [])

  const stopResizing = useCallback(() => {
    setIsDragging(false)
  }, [])

  const handleMouseMove = useCallback((e: MouseEvent) => {
    if (!isDragging) return

    const containerWidth = window.innerWidth
    const newWidth = (e.clientX / containerWidth) * 100

    // Constrain width between min and max
    const constrainedWidth = Math.max(minWidth, Math.min(maxWidth, newWidth))
    
    setWidth(constrainedWidth)
    onResize?.(constrainedWidth)
  }, [isDragging, minWidth, maxWidth, onResize])

  const handleMouseUp = useCallback(() => {
    setIsDragging(false)
  }, [])

  // Add event listeners
  if (typeof window !== 'undefined') {
    if (isDragging) {
      window.addEventListener('mousemove', handleMouseMove)
      window.addEventListener('mouseup', handleMouseUp)
    }
    
    return () => {
      window.removeEventListener('mousemove', handleMouseMove)
      window.removeEventListener('mouseup', handleMouseUp)
    }
  }

  return {
    width,
    isDragging,
    startResizing,
    stopResizing
  }
}
