import { useState, useCallback, useRef } from 'react'
import type { ChatMessage, ResearchConfig } from '@types/index'
import { apiService } from '@services/api'
import { useDebugLogger } from './useDebugLogger'

export const useChat = (sessionId: string) => {
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [streamingMessage, setStreamingMessage] = useState<string>('')
  const [isStreaming, setIsStreaming] = useState(false)
  const abortControllerRef = useRef<AbortController | null>(null)
  
  // Debug logging
  const { logMessage, logApiCall, logApiResponse, logError, logState } = useDebugLogger()

  const loadHistory = useCallback(async () => {
    try {
      setIsLoading(true)
      logApiCall(`/chat/${sessionId}/history`, 'GET', null, 'sent')

      const history = await apiService.getChatHistory(sessionId)

      logApiResponse(`/chat/${sessionId}/history`, 200, history)
      setMessages(history)
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : 'Failed to load chat history'
      setError(errorMsg)
      logError(err as Error, 'loadHistory')
    } finally {
      setIsLoading(false)
    }
  }, [sessionId, logApiCall, logApiResponse, logError])

  const sendMessageStreaming = useCallback(async (content: string, config?: ResearchConfig) => {
    try {
      setIsStreaming(true)
      setError(null)
      setStreamingMessage('')

      // Add user message immediately
      const userMessage: ChatMessage = {
        id: crypto.randomUUID(),
        role: 'user',
        content,
        timestamp: new Date().toISOString(),
        metadata: null
      }
      setMessages(prev => [...prev, userMessage])

      // Log user message
      logMessage(content, 'user', 'sent')

      // Log API call
      logApiCall(`/chat/${sessionId}/stream`, 'POST', { message: content, config }, 'sent')

      let completeMessage = ''

      // Start streaming
      const controller = apiService.streamQuery(
        sessionId,
        content,
        config,
        // onUpdate callback - receives JSON strings
        (update: string) => {
          try {
            // Try to parse as JSON first
            const data = JSON.parse(update)
            if (data.message) {
              // Extract the message from the response object
              completeMessage = data.message
              setStreamingMessage(data.message)
            } else if (data.response) {
              // Alternative key name
              completeMessage = data.response
              setStreamingMessage(data.response)
            }
            logState({ update: data, accumulated: completeMessage }, 'StreamUpdate', 'received')
          } catch {
            // Not JSON, treat as plain text
            completeMessage += update + '\n'
            setStreamingMessage(completeMessage)
            logState({ update, accumulated: completeMessage }, 'StreamUpdate', 'received')
          }
        },
        // onComplete callback
        () => {
          // Save the complete message to history
          const assistantMessage: ChatMessage = {
            id: crypto.randomUUID(),
            role: 'assistant',
            content: completeMessage || streamingMessage,
            timestamp: new Date().toISOString(),
            metadata: { streamed: true, config }
          }
          setMessages(prev => [...prev, assistantMessage])

          // Log complete message
          logMessage(completeMessage || streamingMessage, 'assistant', 'received')
          logApiResponse(`/chat/${sessionId}/stream`, 200, { completed: true })

          setStreamingMessage('')
          setIsStreaming(false)
          abortControllerRef.current = null
        },
        // onError callback
        (error: Error) => {
          setError(error.message)
          logError(error, 'sendMessageStreaming')
          setIsStreaming(false)

          // Save partial message if any
          if (completeMessage || streamingMessage) {
            const partialMessage: ChatMessage = {
              id: crypto.randomUUID(),
              role: 'assistant',
              content: `Error: ${error.message}\n\nPartial response:\n${completeMessage || streamingMessage}`,
              timestamp: new Date().toISOString(),
              metadata: { error: true, streamed: true }
            }
            setMessages(prev => [...prev, partialMessage])
          }

          setStreamingMessage('')
          abortControllerRef.current = null
        }
      )

      abortControllerRef.current = controller
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : 'Failed to send streaming message'
      setError(errorMsg)
      logError(err as Error, 'sendMessageStreaming')
      setIsStreaming(false)
      setStreamingMessage('')
      throw err
    }
  }, [sessionId, logMessage, logApiCall, logApiResponse, logError, logState])

  const sendMessage = useCallback(async (content: string, config?: ResearchConfig) => {
    // Use streaming by default for better UX and real-time updates
    return sendMessageStreaming(content, config)
  }, [sendMessageStreaming])

  const cancelStreaming = useCallback(() => {
    if (abortControllerRef.current) {
      abortControllerRef.current.abort()
      abortControllerRef.current = null
      setIsStreaming(false)
      setStreamingMessage('')
      
      logState({ cancelled: true }, 'StreamCancelled', 'sent')
    }
  }, [logState])

  return { 
    messages, 
    isLoading, 
    error, 
    loadHistory, 
    sendMessage,
    sendMessageStreaming,
    isStreaming,
    streamingMessage,
    cancelStreaming
  }
}
