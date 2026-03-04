/**
 * ResearchProgress Component - Displays real-time research pipeline progress
 * Shows StreamState updates with progress bar, status messages, and content
 */

import React, { useEffect, useState } from 'react'
import { CheckCircle2, Loader2, AlertCircle, Zap } from 'lucide-react'
import type { ResearchProgress, StreamState } from '@types/index'
import {
  getProgressSummary,
  truncateContent,
  formatStreamStateField,
  getCurrentPhase,
} from '@utils/streamStateFormatter'

interface ResearchProgressProps {
  state: StreamState | null
  progress: ResearchProgress
  isStreaming: boolean
  error?: Error | null
  supervisorUpdateCount?: number
}

/**
 * Visual indicator showing current phase in the research pipeline
 */
export const PhaseIndicator: React.FC<{ phase: ResearchProgress['phase']; isStreaming: boolean }> = ({
  phase,
  isStreaming,
}) => {
  const phases: Array<{ id: ResearchProgress['phase']; label: string; icon: React.ReactNode }> = [
    { id: 'clarify', label: 'Clarify', icon: '🔍' },
    { id: 'brief', label: 'Brief', icon: '📝' },
    { id: 'draft', label: 'Draft', icon: '📄' },
    { id: 'supervisor', label: 'Refine', icon: '🔄' },
    { id: 'final', label: 'Final', icon: '✨' },
  ]

  const phaseOrder = ['clarify', 'brief', 'draft', 'supervisor', 'final'] as const
  const currentIndex = phaseOrder.indexOf(phase)

  return (
    <div className="flex items-center justify-between">
      {phases.map((p, index) => (
        <div key={p.id} className="flex flex-col items-center flex-1">
          <div
            className={`w-10 h-10 rounded-full flex items-center justify-center text-lg transition-all ${
              index < currentIndex
                ? 'bg-green-100 text-green-700'
                : index === currentIndex
                  ? isStreaming
                    ? 'bg-blue-100 text-blue-700 animate-pulse'
                    : 'bg-blue-100 text-blue-700'
                  : 'bg-gray-100 text-gray-400'
            }`}
          >
            {index < currentIndex ? '✓' : p.icon}
          </div>
          <p className="text-xs mt-2 font-medium text-gray-600">{p.label}</p>

          {/* Connector line */}
          {index < phases.length - 1 && (
            <div
              className={`absolute left-1/2 top-5 w-12 h-0.5 -translate-x-full transition-colors ${
                index < currentIndex ? 'bg-green-300' : 'bg-gray-200'
              }`}
            />
          )}
        </div>
      ))}
    </div>
  )
}

/**
 * Progress bar component with percentage display
 */
export const ProgressBar: React.FC<{ percentage: number; isStreaming: boolean }> = ({
  percentage,
  isStreaming,
}) => {
  return (
    <div className="w-full">
      <div className="flex items-center justify-between mb-2">
        <span className="text-sm font-medium text-gray-700">Progress</span>
        <span className="text-sm text-gray-600">{Math.round(percentage)}%</span>
      </div>
      <div className="w-full bg-gray-200 rounded-full h-2 overflow-hidden">
        <div
          className={`h-full rounded-full transition-all duration-300 ${
            isStreaming
              ? 'bg-gradient-to-r from-blue-500 to-blue-400 animate-pulse'
              : percentage === 100
                ? 'bg-gradient-to-r from-green-500 to-green-400'
                : 'bg-gradient-to-r from-yellow-500 to-yellow-400'
          }`}
          style={{ width: `${percentage}%` }}
        />
      </div>
    </div>
  )
}

/**
 * Status message display with icon
 */
export const StatusMessage: React.FC<{ message: string; isStreaming: boolean; error?: Error | null }> = ({
  message,
  isStreaming,
  error,
}) => {
  if (error) {
    return (
      <div className="flex items-center gap-2 p-3 bg-red-50 border border-red-200 rounded-lg">
        <AlertCircle className="w-5 h-5 text-red-600 flex-shrink-0" />
        <div>
          <p className="font-medium text-red-900">Error</p>
          <p className="text-sm text-red-700">{error.message}</p>
        </div>
      </div>
    )
  }

  return (
    <div className="flex items-center gap-3 p-3 bg-blue-50 border border-blue-200 rounded-lg">
      {isStreaming ? (
        <Loader2 className="w-5 h-5 text-blue-600 animate-spin flex-shrink-0" />
      ) : (
        <CheckCircle2 className="w-5 h-5 text-green-600 flex-shrink-0" />
      )}
      <p className="text-sm text-blue-900 font-medium">{message}</p>
    </div>
  )
}

/**
 * Content display area showing the main research output
 */
export const ContentDisplay: React.FC<{ content: string; title: string; isStreaming: boolean }> = ({
  content,
  title,
  isStreaming,
}) => {
  if (!content) {
    return null
  }

  return (
    <div className="mt-4 p-4 bg-gray-50 border border-gray-200 rounded-lg">
      <h3 className="font-semibold text-gray-900 mb-2 flex items-center gap-2">
        {isStreaming && <Loader2 className="w-4 h-4 animate-spin" />}
        {title}
      </h3>
      <p className="text-sm text-gray-700 whitespace-pre-wrap leading-relaxed">{truncateContent(content, 500)}</p>
    </div>
  )
}

/**
 * Supervisor update list showing iterative refinement steps
 */
export const SupervisorUpdates: React.FC<{ updates: string[]; count: number; isStreaming: boolean }> = ({
  updates,
  count,
  isStreaming,
}) => {
  if (count === 0) {
    return null
  }

  return (
    <div className="mt-4 p-4 bg-amber-50 border border-amber-200 rounded-lg">
      <h3 className="font-semibold text-amber-900 mb-3 flex items-center gap-2">
        <Zap className="w-4 h-4" />
        Refinement Progress ({count} updates)
      </h3>
      <div className="space-y-2 max-h-48 overflow-y-auto">
        {updates.slice(-5).map((update, index) => (
          <div key={index} className="text-xs text-amber-800 flex items-start gap-2">
            <span className="inline-block w-5 h-5 rounded-full bg-amber-300 text-amber-900 flex items-center justify-center flex-shrink-0">
              {updates.length - 4 + index}
            </span>
            <span>{truncateContent(update, 200)}</span>
          </div>
        ))}
        {isStreaming && updates.length > 5 && (
          <p className="text-xs text-amber-600 italic">... and more updates</p>
        )}
      </div>
    </div>
  )
}

/**
 * Main ResearchProgress component
 */
export const ResearchProgressCard: React.FC<ResearchProgressProps> = ({
  state,
  progress,
  isStreaming,
  error,
  supervisorUpdateCount,
}) => {
  const [supervisorUpdates, setSupervisorUpdates] = useState<string[]>([])

  // Track supervisor updates
  useEffect(() => {
    if (state?.supervisorUpdate) {
      setSupervisorUpdates((prev) => [...prev, state.supervisorUpdate as string])
    }
  }, [state?.supervisorUpdate])

  const summary = state ? getProgressSummary(state) : 'Ready to start research...'

  return (
    <div className="w-full space-y-4 p-6 bg-white border border-gray-200 rounded-lg shadow-sm">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold text-gray-900">Research Progress</h2>
        {state?.researchId && (
          <code className="text-xs px-2 py-1 bg-gray-100 text-gray-600 rounded font-mono">
            ID: {state.researchId.substring(0, 8)}...
          </code>
        )}
      </div>

      {/* Summary */}
      {summary && (
        <div className="text-sm text-gray-600 p-2 bg-gray-50 rounded border border-gray-100">
          {summary}
        </div>
      )}

      {/* Phase Indicator */}
      <div className="relative py-8">
        <PhaseIndicator phase={progress.phase} isStreaming={isStreaming} />
      </div>

      {/* Progress Bar */}
      <ProgressBar percentage={progress.percentage} isStreaming={isStreaming} />

      {/* Status Message */}
      <StatusMessage message={progress.message} isStreaming={isStreaming} error={error} />

      {/* Research Brief */}
      {state?.researchBrief && (
        <ContentDisplay
          content={state.researchBrief}
          title="Research Brief"
          isStreaming={isStreaming && !state.draftReport}
        />
      )}

      {/* Draft Report */}
      {state?.draftReport && (
        <ContentDisplay
          content={state.draftReport}
          title="Draft Report"
          isStreaming={isStreaming && supervisorUpdateCount === 0}
        />
      )}

      {/* Supervisor Updates */}
      <SupervisorUpdates updates={supervisorUpdates} count={supervisorUpdateCount || 0} isStreaming={isStreaming} />

      {/* Final Report */}
      {state?.finalReport && (
        <ContentDisplay
          content={state.finalReport}
          title="Final Report ✅"
          isStreaming={false}
        />
      )}
    </div>
  )
}

export default ResearchProgressCard
