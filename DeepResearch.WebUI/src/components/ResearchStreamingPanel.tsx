/**
 * ResearchStreamingPanel Component
 * Integrates the new MasterWorkflow streaming endpoint into the UI
 * Uses: useMasterWorkflowStream hook + ResearchProgressCard component
 */

import React, { useState } from 'react'
import { ArrowRight, Loader2 } from 'lucide-react'
import { useMasterWorkflowStream } from '@hooks/useMasterWorkflowStream'
import ResearchProgressCard from './ResearchProgressCard'

interface ResearchStreamingPanelProps {
  onQueryStart?: (query: string) => void
  onQueryComplete?: () => void
}

export default function ResearchStreamingPanel({
  onQueryStart,
  onQueryComplete,
}: ResearchStreamingPanelProps) {
  const { currentState, progress, isStreaming, error, startStream, reset } = useMasterWorkflowStream()
  const [query, setQuery] = useState('')

  const handleResearch = async () => {
    if (!query.trim()) return

    try {
      onQueryStart?.(query)
      await startStream(query)
      setQuery('')
      onQueryComplete?.()
    } catch (err) {
      console.error('Research error:', err)
    }
  }

  const handleReset = () => {
    reset()
    setQuery('')
  }

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      handleResearch()
    }
  }

  return (
    <div className="flex flex-col h-full gap-4 p-4 bg-white dark:bg-gray-900">
      {/* Header */}
      <div className="border-b border-gray-200 dark:border-gray-700 pb-4">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-white mb-2">
          Research Agent
        </h2>
        <p className="text-sm text-gray-600 dark:text-gray-400">
          Enter your research query to begin real-time analysis
        </p>
      </div>

      {/* Input Section */}
      <div className="flex gap-2">
        <textarea
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="What would you like to research?&#10;Example: How much would it cost to send satellites to Jupiter?"
          disabled={isStreaming}
          className="flex-1 px-4 py-3 border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-800 text-gray-900 dark:text-white placeholder-gray-500 dark:placeholder-gray-400 resize-none focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed"
          rows={3}
        />
      </div>

      {/* Button Row */}
      <div className="flex gap-2">
        <button
          onClick={handleResearch}
          disabled={isStreaming || !query.trim()}
          className="flex items-center gap-2 px-6 py-3 bg-blue-600 hover:bg-blue-700 disabled:bg-gray-400 disabled:cursor-not-allowed text-white rounded-lg font-medium transition-colors"
        >
          {isStreaming ? (
            <>
              <Loader2 className="w-4 h-4 animate-spin" />
              Researching...
            </>
          ) : (
            <>
              <ArrowRight className="w-4 h-4" />
              Research
            </>
          )}
        </button>

        {(isStreaming || currentState) && (
          <button
            onClick={handleReset}
            disabled={isStreaming}
            className="px-6 py-3 bg-gray-300 hover:bg-gray-400 disabled:bg-gray-200 disabled:cursor-not-allowed text-gray-900 rounded-lg font-medium transition-colors"
          >
            Clear
          </button>
        )}
      </div>

      {/* Progress Display */}
      {(currentState || isStreaming) && (
        <div className="flex-1 overflow-auto">
          <ResearchProgressCard
            state={currentState}
            progress={progress}
            isStreaming={isStreaming}
            error={error}
            supervisorUpdateCount={currentState?.supervisorUpdateCount}
          />
        </div>
      )}

      {/* Empty State */}
      {!currentState && !isStreaming && (
        <div className="flex-1 flex items-center justify-center text-center">
          <div className="space-y-4">
            <div className="text-5xl">🔍</div>
            <p className="text-gray-600 dark:text-gray-400">
              Enter a research query to begin
            </p>
            <p className="text-sm text-gray-500 dark:text-gray-500">
              The system will conduct a comprehensive analysis through multiple phases:
            </p>
            <ul className="text-sm text-gray-600 dark:text-gray-400 space-y-1">
              <li>• 🎯 Clarify your query intent</li>
              <li>• 📋 Create research brief</li>
              <li>• 📄 Generate draft report</li>
              <li>• 🔄 Refine through iterations</li>
              <li>• ✨ Produce final report</li>
            </ul>
          </div>
        </div>
      )}

      {/* Error Display */}
      {error && (
        <div className="p-4 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg">
          <p className="text-red-800 dark:text-red-200 font-medium">Error</p>
          <p className="text-sm text-red-700 dark:text-red-300 mt-1">{error.message}</p>
          <button
            onClick={handleReset}
            className="mt-3 text-sm text-red-600 dark:text-red-400 hover:underline font-medium"
          >
            Try Again
          </button>
        </div>
      )}
    </div>
  )
}
