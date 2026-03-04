import { useState, useCallback, useEffect, useRef } from 'react'

interface UseResizableOptions {
  initialHeight: number
  minHeight: number
  maxHeight: number
  onResize?: (height: number) => void
}

export const useResizable = ({
  initialHeight,
  minHeight,
  maxHeight,
  onResize,
}: UseResizableOptions) => {
  const [height, setHeight] = useState(initialHeight)
  const [isDragging, setIsDragging] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)

  const startDragging = useCallback(() => {
    setIsDragging(true)
  }, [])

  const stopDragging = useCallback(() => {
    setIsDragging(false)
  }, [])

  const handleMouseMove = useCallback(
    (e: MouseEvent) => {
      if (!isDragging || !containerRef.current) return

      const containerRect = containerRef.current.getBoundingClientRect()
      const newHeight = ((containerRect.bottom - e.clientY) / containerRect.height) * 100

      const clampedHeight = Math.max(minHeight, Math.min(maxHeight, newHeight))
      setHeight(clampedHeight)
      onResize?.(clampedHeight)
    },
    [isDragging, minHeight, maxHeight, onResize]
  )

  useEffect(() => {
    if (isDragging) {
      document.addEventListener('mousemove', handleMouseMove)
      document.addEventListener('mouseup', stopDragging)

      return () => {
        document.removeEventListener('mousemove', handleMouseMove)
        document.removeEventListener('mouseup', stopDragging)
      }
    }
  }, [isDragging, handleMouseMove, stopDragging])

  return {
    height,
    isDragging,
    containerRef,
    startDragging,
    setHeight,
  }
}
