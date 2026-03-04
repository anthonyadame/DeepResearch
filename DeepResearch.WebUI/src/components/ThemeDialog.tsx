import { X, Sun, Moon, Monitor } from 'lucide-react'
import { useTheme } from '@contexts/ThemeContext'

interface ThemeDialogProps {
  onClose: () => void
}

export default function ThemeDialog({ onClose }: ThemeDialogProps) {
  const { theme, setTheme } = useTheme()

  const themes = [
    { id: 'light' as const, name: 'Light', icon: Sun, description: 'Light theme' },
    { id: 'dark' as const, name: 'Dark', icon: Moon, description: 'Dark theme' },
    { id: 'system' as const, name: 'System', icon: Monitor, description: 'Use system preference' }
  ]

  const handleSelect = (themeId: 'light' | 'dark' | 'system') => {
    setTheme(themeId)
    onClose()
  }

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div className="bg-white dark:bg-gray-800 rounded-lg shadow-xl w-full max-w-md mx-4">
        {/* Header */}
        <div className="flex items-center justify-between p-4 border-b border-gray-200 dark:border-gray-700">
          <h3 className="text-lg font-semibold text-gray-800 dark:text-white">Choose Theme</h3>
          <button
            onClick={onClose}
            className="p-1 hover:bg-gray-100 dark:hover:bg-gray-700 rounded transition-colors"
          >
            <X className="w-5 h-5 text-gray-500 dark:text-gray-400" />
          </button>
        </div>

        {/* Content */}
        <div className="p-4 space-y-2">
          {themes.map((themeOption) => {
            const Icon = themeOption.icon
            return (
              <button
                key={themeOption.id}
                onClick={() => handleSelect(themeOption.id)}
                className={`w-full p-4 rounded-lg border-2 transition-all text-left ${
                  theme === themeOption.id
                    ? 'border-blue-500 bg-blue-50 dark:bg-blue-900/20'
                    : 'border-gray-200 dark:border-gray-700 hover:border-gray-300 dark:hover:border-gray-600'
                }`}
              >
                <div className="flex items-start gap-3">
                  <Icon className="w-5 h-5 text-gray-600 dark:text-gray-300 mt-0.5" />
                  <div>
                    <div className="font-medium text-gray-800 dark:text-white">{themeOption.name}</div>
                    <div className="text-sm text-gray-500 dark:text-gray-400">{themeOption.description}</div>
                  </div>
                  {theme === themeOption.id && (
                    <div className="ml-auto">
                      <div className="w-2 h-2 bg-blue-500 rounded-full"></div>
                    </div>
                  )}
                </div>
              </button>
            )
          })}
        </div>
      </div>
    </div>
  )
}
