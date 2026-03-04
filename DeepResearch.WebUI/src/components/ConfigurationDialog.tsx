import React, { useState } from 'react'
import { X } from 'lucide-react'
import type { ResearchConfig } from '@types/index'

interface ConfigurationDialogProps {
  config?: ResearchConfig
  onSave: (config: ResearchConfig) => void
  onClose: () => void
}

export default function ConfigurationDialog({ config, onSave, onClose }: ConfigurationDialogProps) {
  const [formData, setFormData] = useState<ResearchConfig>(
    config || {
      language: 'en',
      llmModels: [],
      includedWebsites: [],
      excludedWebsites: [],
      topics: [],
    }
  )

  const handleSave = () => {
    onSave(formData)
  }

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
      <div className="bg-white rounded-lg shadow-xl w-full max-w-2xl max-h-96 overflow-y-auto">
        {/* Header */}
        <div className="flex items-center justify-between p-6 border-b border-gray-200">
          <h2 className="text-xl font-semibold">Research Configuration</h2>
          <button onClick={onClose} className="p-1 hover:bg-gray-100 rounded">
            <X className="w-6 h-6" />
          </button>
        </div>

        {/* Content */}
        <div className="p-6 space-y-4">
          {/* Language */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              Language
            </label>
            <select
              value={formData.language}
              onChange={(e) => setFormData({ ...formData, language: e.target.value })}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option value="en">English</option>
              <option value="es">Spanish</option>
              <option value="fr">French</option>
              <option value="de">German</option>
            </select>
          </div>

          {/* Topics */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              Topics (comma-separated)
            </label>
            <textarea
              value={formData.topics.join(', ')}
              onChange={(e) => setFormData({
                ...formData,
                topics: e.target.value.split(',').map(t => t.trim())
              })}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
              rows={3}
            />
          </div>

          {/* Included Websites */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              Include Websites (comma-separated)
            </label>
            <textarea
              value={formData.includedWebsites.join(', ')}
              onChange={(e) => setFormData({
                ...formData,
                includedWebsites: e.target.value.split(',').map(t => t.trim())
              })}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
              rows={2}
            />
          </div>

          {/* Excluded Websites */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              Exclude Websites (comma-separated)
            </label>
            <textarea
              value={formData.excludedWebsites.join(', ')}
              onChange={(e) => setFormData({
                ...formData,
                excludedWebsites: e.target.value.split(',').map(t => t.trim())
              })}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500"
              rows={2}
            />
          </div>
        </div>

        {/* Footer */}
        <div className="flex justify-end gap-2 p-6 border-t border-gray-200">
          <button
            onClick={onClose}
            className="px-4 py-2 text-gray-700 border border-gray-300 rounded-lg hover:bg-gray-50"
          >
            Cancel
          </button>
          <button
            onClick={handleSave}
            className="px-4 py-2 bg-blue-500 text-white rounded-lg hover:bg-blue-600"
          >
            Save
          </button>
        </div>
      </div>
    </div>
  )
}
