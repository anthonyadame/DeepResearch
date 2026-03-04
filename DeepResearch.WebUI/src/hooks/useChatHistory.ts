import { useState, useEffect, useCallback } from 'react'
import type { ChatSession } from '../types'
import { apiService } from '@services/api'

export const useChatHistory = () => {
  const [sessions, setSessions] = useState<ChatSession[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const loadSessions = useCallback(async () => {
    try {
      setIsLoading(true)
      setError(null)
      const data = await apiService.getSessions()
      setSessions(data)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load sessions')
    } finally {
      setIsLoading(false)
    }
  }, [])

  const deleteSession = useCallback(async (sessionId: string) => {
    try {
      await apiService.deleteSession(sessionId)
      setSessions(prev => prev.filter(s => s.id !== sessionId))
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete session')
      throw err
    }
  }, [])

  const createSession = useCallback(async (title?: string) => {
    try {
      const newSession = await apiService.createSession(title)
      setSessions(prev => [newSession, ...prev])
      return newSession
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create session')
      throw err
    }
  }, [])

  useEffect(() => {
    loadSessions()
  }, [loadSessions])

  return {
    sessions,
    isLoading,
    error,
    loadSessions,
    deleteSession,
    createSession
  }
}
