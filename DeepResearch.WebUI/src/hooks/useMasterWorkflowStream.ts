/**
 * React hook for consuming MasterWorkflow streams
 * Handles state management, progress tracking, and error handling
 */

import { useEffect, useState, useCallback, useRef } from 'react'
import type { StreamState, ResearchProgress } from '@types/index'
import {
  getCurrentPhase,
  calculateProgress,
  getProgressMessage,
  getPhaseContent,
  streamStateToProgress,
} from '@utils/streamStateFormatter'
import { MasterWorkflowStreamClient } from '@services/masterWorkflowStreamClient'

interface UseMasterWorkflowStreamOptions {
  autoStart?: boolean
  timeout?: number
}

interface UseMasterWorkflowStreamReturn {
  states: StreamState[]
  currentState: StreamState | null
  progress: ResearchProgress
  isStreaming: boolean
  error: Error | null
  startStream: (query: string) => Promise<void>
  cancelStream: () => void
  reset: () => void
}

/**
 * Hook for streaming research from MasterWorkflow API
 *
 * @example
 * const { states, progress, isStreaming, startStream } = useMasterWorkflowStream()
 *
 * // Start streaming
 * await startStream("What is AI?")
 *
 * // Display progress
 * <ProgressBar value={progress.percentage} />
 * <p>{progress.message}</p>
 */
export function useMasterWorkflowStream(
  options: UseMasterWorkflowStreamOptions = {}
): UseMasterWorkflowStreamReturn {
  const [states, setStates] = useState<StreamState[]>([])
  const [currentState, setCurrentState] = useState<StreamState | null>(null)
  const [isStreaming, setIsStreaming] = useState(false)
  const [error, setError] = useState<Error | null>(null)

  const clientRef = useRef(new MasterWorkflowStreamClient())

  const reset = useCallback(() => {
    setStates([])
    setCurrentState(null)
    setError(null)
    setIsStreaming(false)
  }, [])

  const cancelStream = useCallback(() => {
    clientRef.current.cancel()
    setIsStreaming(false)
  }, [])

  const startStream = useCallback(
    async (query: string) => {
      if (!query || !query.trim()) {
        const err = new Error('Query cannot be empty')
        setError(err)
        throw err
      }

      // Reset previous state
      reset()
      setIsStreaming(true)
      setError(null)

      try {
        await clientRef.current.streamMasterWorkflow(query, {
          onStateReceived: (state) => {
            setStates((prev) => [...prev, state])
            setCurrentState(state)
          },
          onError: (err) => {
            setError(err)
            setIsStreaming(false)
          },
          onComplete: () => {
            setIsStreaming(false)
          },
          timeout: options.timeout,
        })
      } catch (err) {
        const error = err instanceof Error ? err : new Error('Unknown error')
        setError(error)
        setIsStreaming(false)
        throw error
      }
    },
    [options.timeout, reset]
  )

  // Calculate progress from current state
  const progress: ResearchProgress = currentState
    ? streamStateToProgress(currentState)
    : {
        phase: 'clarify',
        percentage: 0,
        message: 'Ready to start research...',
        content: '',
      }

  return {
    states,
    currentState,
    progress,
    isStreaming,
    error,
    startStream,
    cancelStream,
    reset,
  }
}

/**
 * Hook for getting final report from a query
 * Simpler than useMasterWorkflowStream when you only need the final result
 *
 * @example
 * const { report, isLoading, error } = useFinalReport()
 * const result = await report("What is AI?")
 */
export function useFinalReport() {
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<Error | null>(null)

  const clientRef = useRef(new MasterWorkflowStreamClient())

  const getFinalReport = useCallback(async (query: string): Promise<string | null> => {
    setIsLoading(true)
    setError(null)

    try {
      const report = await clientRef.current.getFinallReport(query)
      return report
    } catch (err) {
      const error = err instanceof Error ? err : new Error('Failed to get report')
      setError(error)
      throw error
    } finally {
      setIsLoading(false)
    }
  }, [])

  return { getFinalReport, isLoading, error }
}

/**
 * Hook for tracking streaming progress with visual updates
 * Returns hooks for the current state so you can render without re-subscribing
 *
 * @example
 * const { phase, percentage, message } = useStreamingProgress(currentState)
 */
export function useStreamingProgress(state: StreamState | null) {
  return {
    phase: state ? getCurrentPhase(state) : 'clarify',
    percentage: state ? calculateProgress(state) : 0,
    message: state ? getProgressMessage(state) : 'Ready to start...',
    content: state ? getPhaseContent(state) : '',
  }
}
