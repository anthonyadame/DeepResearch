import { useCallback } from 'react'
import { useDebugStore } from '@stores/debugStore'
import type { DebugMessage } from '@stores/debugStore'

/**
 * Hook to capture and log all messages, states, and API calls to the debug console
 */
export const useDebugLogger = () => {
  const addDebugMessage = useDebugStore((state) => state.addDebugMessage)

  const logMessage = useCallback(
    (content: string, role: 'user' | 'assistant' | 'system', direction: 'sent' | 'received') => {
      addDebugMessage({
        direction,
        type: 'message',
        data: {
          role,
          content,
          timestamp: new Date().toISOString(),
        },
      })
    },
    [addDebugMessage]
  )

  const logState = useCallback(
    (state: any, label: string, direction: 'sent' | 'received') => {
      addDebugMessage({
        direction,
        type: 'state',
        data: {
          label,
          state,
          timestamp: new Date().toISOString(),
        },
      })
    },
    [addDebugMessage]
  )

  const logApiCall = useCallback(
    (endpoint: string, method: string, requestData: any, direction: 'sent') => {
      addDebugMessage({
        direction,
        type: 'api_call',
        endpoint,
        data: {
          method,
          endpoint,
          requestData,
          timestamp: new Date().toISOString(),
        },
      })
    },
    [addDebugMessage]
  )

  const logApiResponse = useCallback(
    (endpoint: string, statusCode: number, responseData: any) => {
      addDebugMessage({
        direction: 'received',
        type: 'api_call',
        endpoint,
        statusCode,
        data: {
          endpoint,
          statusCode,
          responseData,
          timestamp: new Date().toISOString(),
        },
      })
    },
    [addDebugMessage]
  )

  const logError = useCallback(
    (error: Error, context?: string) => {
      addDebugMessage({
        direction: 'received',
        type: 'error',
        data: {
          message: error.message,
          stack: error.stack,
          context,
          timestamp: new Date().toISOString(),
        },
      })
    },
    [addDebugMessage]
  )

  return {
    logMessage,
    logState,
    logApiCall,
    logApiResponse,
    logError,
  }
}
