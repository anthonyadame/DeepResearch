import { useEffect, useRef } from 'react'

interface DropdownItem {
  icon: React.ReactNode
  label: string
  onClick: () => void
}

interface DropdownMenuProps {
  items: DropdownItem[]
  onClose: () => void
}

export default function DropdownMenu({ items, onClose }: DropdownMenuProps) {
  const menuRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(event.target as Node)) {
        onClose()
      }
    }

    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        onClose()
      }
    }

    document.addEventListener('mousedown', handleClickOutside)
    document.addEventListener('keydown', handleEscape)
    
    return () => {
      document.removeEventListener('mousedown', handleClickOutside)
      document.removeEventListener('keydown', handleEscape)
    }
  }, [onClose])

  return (
    <div
      ref={menuRef}
      className="absolute bottom-full left-0 mb-2 w-56 bg-white dark:bg-gray-800 rounded-lg shadow-lg border border-gray-200 dark:border-gray-700 py-2 z-50">
      {items.map((item, index) => (
        <button
          key={index}
          onClick={item.onClick}
          className="w-full px-4 py-2.5 text-left hover:bg-gray-50 dark:hover:bg-gray-700 transition-colors flex items-center gap-3 text-sm text-gray-700 dark:text-gray-200">
          <span className="text-gray-500 dark:text-gray-400">{item.icon}</span>
          <span>{item.label}</span>
        </button>
      ))}
    </div>
  )
}
