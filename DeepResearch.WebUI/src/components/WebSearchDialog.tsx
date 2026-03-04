import { useState } from 'react'
import { X, Search, Globe, Check } from 'lucide-react'

interface WebSearchDialogProps {
  currentProvider: string
  onClose: () => void
  onSelect: (provider: string) => void
}

export default function WebSearchDialog({ currentProvider, onClose, onSelect }: WebSearchDialogProps) {
  const [selectedProvider, setSelectedProvider] = useState(currentProvider)

  const providers = [
    { 
      id: 'searxng', 
      name: 'SearXNG', 
      description: 'Privacy-focused metasearch engine',
      recommended: true
    },
    { 
      id: 'google', 
      name: 'Google', 
      description: 'Google Search API',
      recommended: false
    },
    { 
      id: 'bing', 
      name: 'Bing', 
      description: 'Microsoft Bing Search',
      recommended: false
    },
    { 
      id: 'duckduckgo', 
      name: 'DuckDuckGo', 
      description: 'Privacy-focused search',
      recommended: false
    }
  ]

  const handleSelect = () => {
    onSelect(selectedProvider)
  }

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div className="bg-white dark:bg-gray-800 rounded-lg shadow-xl w-full max-w-md mx-4">
        {/* Header */}
        <div className="flex items-center justify-between p-4 border-b border-gray-200 dark:border-gray-700">
          <div className="flex items-center gap-2">
            <Globe className="w-5 h-5 text-gray-600 dark:text-gray-400" />
            <h3 className="text-lg font-semibold text-gray-800 dark:text-white">Select Web Search Provider</h3>
          </div>
          <button
            onClick={onClose}
            className="p-1 hover:bg-gray-100 dark:hover:bg-gray-700 rounded transition-colors"
          >
            <X className="w-5 h-5 text-gray-500 dark:text-gray-400" />
          </button>
        </div>

        {/* Content */}
        <div className="p-4 space-y-2 max-h-96 overflow-y-auto">
          {providers.map((provider) => (
            <button
              key={provider.id}
              onClick={() => setSelectedProvider(provider.id)}
              className={`w-full p-3 rounded-lg border-2 transition-all text-left ${
                selectedProvider === provider.id
                  ? 'border-blue-500 bg-blue-50 dark:bg-blue-900/20'
                  : 'border-gray-200 dark:border-gray-700 hover:border-gray-300 dark:hover:border-gray-600'
              }`}
            >
              <div className="flex items-start gap-3">
                <Search className="w-5 h-5 text-gray-600 dark:text-gray-400 mt-0.5 flex-shrink-0" />
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2">
                    <span className="font-medium text-gray-800 dark:text-white">{provider.name}</span>
                    {provider.recommended && (
                      <span className="text-xs bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-400 px-2 py-0.5 rounded">
                        Recommended
                      </span>
                    )}
                  </div>
                  <div className="text-sm text-gray-500 dark:text-gray-400 mt-1">{provider.description}</div>
                </div>
                {selectedProvider === provider.id && (
                  <Check className="w-5 h-5 text-blue-500 flex-shrink-0" />
                )}
              </div>
            </button>
          ))}
        </div>

        {/* Footer */}
        <div className="flex justify-end gap-2 p-4 border-t border-gray-200 dark:border-gray-700">
          <button
            onClick={onClose}
            className="px-4 py-2 text-gray-600 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-lg transition-colors"
          >
            Cancel
          </button>
          <button
            onClick={handleSelect}
            className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors"
          >
            Apply Selection
          </button>
        </div>

        {/* Info Note */}
        <div className="px-4 pb-4">
          <div className="bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 rounded-lg p-3">
            <p className="text-xs text-blue-700 dark:text-blue-300">
              <strong>Note:</strong> Selected provider will be used for web searches during research sessions. 
              You can change this at any time.
            </p>
          </div>
        </div>
      </div>
    </div>
  )
}