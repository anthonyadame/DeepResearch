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

  const sendMessage = useCallback(async (content: string, config?: ResearchConfig) => {
    try {
      setIsLoading(true)
      setError(null)
      
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
      logApiCall(`/chat/${sessionId}/query`, 'POST', { message: content, config }, 'sent')

      const response = await apiService.submitQuery(sessionId, content, config)
      
      // Log API response
      logApiResponse(`/chat/${sessionId}/query`, 200, response)
      logMessage(response.content, 'assistant', 'received')
      
      setMessages(prev => [...prev, response])
      return response
    } catch (err) {
      const errorMsg = err instanceof Error ? err.message : 'Failed to send message'
      setError(errorMsg)
      logError(err as Error, 'sendMessage')
      throw err
    } finally {
      setIsLoading(false)
    }
  }, [sessionId, logMessage, logApiCall, logApiResponse, logError])

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

      // Start streaming
      const controller = apiService.streamQuery(
        sessionId,
        content,
        config,
        // onUpdate callback
        (update: string) => {
          setStreamingMessage(prev => prev + update + '\n')
          // Log streaming updates as state
          logState({ update, accumulated: streamingMessage + update }, 'StreamUpdate', 'received')
        },
        // onComplete callback
        () => {
          // Save the complete message to history
          const assistantMessage: ChatMessage = {
            id: crypto.randomUUID(),
            role: 'assistant',
            content: streamingMessage,
            timestamp: new Date().toISOString(),
            metadata: { streamed: true, config }
          }
          setMessages(prev => [...prev, assistantMessage])
          
          // Log complete message
          logMessage(streamingMessage, 'assistant', 'received')
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
  }, [sessionId, streamingMessage, logMessage, logApiCall, logApiResponse, logError, logState])

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
