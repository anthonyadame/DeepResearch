import axios, { AxiosInstance } from 'axios'
import type { ChatMessage, ChatSession, ResearchConfig, StreamState } from '@types/index'

class ApiService {
  private client: AxiosInstance
  private baseURL: string

  constructor(baseURL: string = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api') {
    this.baseURL = baseURL
    this.client = axios.create({
      baseURL,
      timeout: 30000,
      headers: {
        'Content-Type': 'application/json',
      },
    })

    // Response interceptor for error handling
    this.client.interceptors.response.use(
      response => response,
      error => {
        console.error('API Error:', error.response?.data || error.message)
        return Promise.reject(error)
      }
    )
  }

  // ============================================
  // NEW: MasterWorkflow Streaming Endpoint
  // ============================================
  /**
   * Stream the MasterWorkflow with real-time progress updates via Server-Sent Events
   * Returns StreamState objects as research progresses through all 5 phases
   *
   * @param userQuery - Research query from user
   * @param onStateReceived - Callback for each StreamState object
   * @param onComplete - Callback when stream completes
   * @param onError - Callback for any streaming errors
   * @returns AbortController to cancel the stream
   */
  streamMasterWorkflow(
    userQuery: string,
    onStateReceived: (state: StreamState) => void,
    onComplete: () => void,
    onError: (error: Error) => void
  ): AbortController {
    const abortController = new AbortController()
    const url = `${this.baseURL}/workflows/master/stream`

    fetch(url, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ userQuery }),
      signal: abortController.signal,
    })
      .then(async (response) => {
        if (!response.ok) {
          throw new Error(`HTTP error! status: ${response.status}`)
        }

        const reader = response.body?.getReader()
        if (!reader) {
          throw new Error('Response body is not readable')
        }

        const decoder = new TextDecoder()
        let buffer = ''

        try {
          while (true) {
            const { done, value } = await reader.read()

            if (done) {
              onComplete()
              break
            }

            buffer += decoder.decode(value, { stream: true })
            const lines = buffer.split('\n')

            // Keep the last line in buffer if it's incomplete
            buffer = lines.pop() || ''

            for (const line of lines) {
              if (line.startsWith('data: ')) {
                const jsonStr = line.substring(6).trim()

                if (!jsonStr) {
                  continue
                }

                if (jsonStr === '[DONE]' || jsonStr === '{"status":"completed"}') {
                  onComplete()
                  return
                }

                try {
                  const state: StreamState = JSON.parse(jsonStr)
                  onStateReceived(state)
                } catch (parseError) {
                  console.error('Failed to parse StreamState JSON:', jsonStr, parseError)
                }
              }
            }
          }
        } catch (error) {
          if (error instanceof Error && error.name === 'AbortError') {
            console.log('Stream aborted by user')
          } else {
            onError(error instanceof Error ? error : new Error('Stream error'))
          }
        } finally {
          reader.releaseLock()
        }
      })
      .catch((error) => {
        if (error.name === 'AbortError') {
          console.log('Fetch aborted')
        } else {
          onError(error)
        }
      })

    return abortController
  }

  // ============================================
  // EXISTING: Chat Endpoints (Updated for new API)
  // ============================================
  async submitQuery(sessionId: string, message: string, config?: ResearchConfig): Promise<ChatMessage> {
    const response = await this.client.post(`/chat/sessions/${sessionId}/query`, {
      message,
      config,
    })
    return response.data
  }

  /**
   * Stream a query to the chat endpoint with Server-Sent Events (SSE)
   * @param sessionId - The chat session ID
   * @param message - The user's message
   * @param config - Optional research configuration
   * @param onUpdate - Callback for each streaming update
   * @param onComplete - Callback when streaming completes
   * @param onError - Callback for errors
   * @returns AbortController to cancel the stream
   */
  streamQuery(
    sessionId: string,
    message: string,
    config: ResearchConfig | undefined,
    onUpdate: (update: string) => void,
    onComplete: () => void,
    onError: (error: Error) => void
  ): AbortController {
    const abortController = new AbortController()
    const url = `${this.baseURL}/chat/sessions/${sessionId}/stream`

    console.log('[ApiService] Starting stream to:', url)

    fetch(url, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ message, config }),
      signal: abortController.signal,
    })
      .then(async (response) => {
        console.log('[ApiService] Stream response status:', response.status)

        if (!response.ok) {
          const errorText = await response.text().catch(() => `HTTP ${response.status}`)
          console.error('[ApiService] Stream request failed:', errorText)
          throw new Error(`HTTP error! status: ${response.status}: ${errorText}`)
        }

        const reader = response.body?.getReader()
        if (!reader) {
          console.error('[ApiService] Response body is not readable')
          throw new Error('Response body is not readable')
        }

        const decoder = new TextDecoder()
        let buffer = ''

        try {
          while (true) {
            const { done, value } = await reader.read()

            if (done) {
              console.log('[ApiService] Stream completed (done=true)')
              onComplete()
              break
            }

            const chunk = decoder.decode(value, { stream: true })
            buffer += chunk
            const lines = buffer.split('\n')

            // Keep the last incomplete line in buffer
            buffer = lines.pop() || ''

            for (const line of lines) {
              if (line.startsWith('data: ')) {
                const data = line.substring(6).trim()

                console.log('[ApiService] Received data:', data.substring(0, 100) + (data.length > 100 ? '...' : ''))

                if (!data) {
                  continue
                }

                if (data === '[DONE]') {
                  console.log('[ApiService] Stream received [DONE]')
                  onComplete()
                  return
                } else if (data === '[CANCELLED]') {
                  console.warn('[ApiService] Stream was cancelled')
                  onError(new Error('Stream was cancelled'))
                  return
                } else if (data.startsWith('{')) {
                  try {
                    const obj = JSON.parse(data)
                    if (obj.error) {
                      console.error('[ApiService] Received error object:', obj)
                      onError(new Error(obj.error))
                      return
                    }
                    // Pass the raw JSON string to the callback
                    onUpdate(data)
                  } catch (parseError) {
                    console.warn('[ApiService] Failed to parse data:', data, parseError)
                    onUpdate(data)
                  }
                } else {
                  onUpdate(data)
                }
              }
            }
          }
        } catch (error) {
          if (error instanceof Error && error.name === 'AbortError') {
            console.log('[ApiService] Stream aborted by user')
          } else {
            const errorMessage = error instanceof Error ? error.message : String(error)
            console.error('[ApiService] Stream error:', errorMessage)
            onError(error instanceof Error ? error : new Error('Stream error: ' + String(error)))
          }
        } finally {
          reader.releaseLock()
        }
      })
      .catch((error) => {
        if (error instanceof Error && error.name === 'AbortError') {
          console.log('[ApiService] Fetch aborted')
        } else {
          const errorMessage = error instanceof Error ? error.message : String(error)
          console.error('[ApiService] Fetch error:', errorMessage)
          onError(error instanceof Error ? error : new Error('Fetch error: ' + String(error)))
        }
      })

    return abortController
  }

  async getChatHistory(sessionId: string): Promise<ChatMessage[]> {
    const response = await this.client.get(`/chat/sessions/${sessionId}/history`)
    return response.data
  }

  /**
   * Check if a session exists without loading full history
   * @param sessionId - The session ID to check
   * @returns true if session exists, false otherwise
   */
  async sessionExists(sessionId: string): Promise<boolean> {
    try {
      const response = await this.client.get(`/chat/sessions/${sessionId}`)
      return response.status === 200
    } catch (error) {
      return false
    }
  }

  async createSession(title?: string): Promise<ChatSession> {
    console.log('[apiService] Creating session with title:', title)
    const response = await this.client.post('/chat/sessions', { title })
    console.log('[apiService] Response status:', response.status)
    console.log('[apiService] Response data:', response.data)
    console.log('[apiService] Response headers:', response.headers)
    return response.data
  }

  async getSessions(): Promise<ChatSession[]> {
    const response = await this.client.get('/chat/sessions')
    return response.data
  }

  async deleteSession(sessionId: string): Promise<void> {
    await this.client.delete(`/chat/sessions/${sessionId}`)
  }

  // File upload
  async uploadFile(sessionId: string, file: File): Promise<{ id: string; name: string }> {
    const formData = new FormData()
    formData.append('file', file)
    const response = await this.client.post(`/chat/sessions/${sessionId}/files`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    return response.data
  }

  // Configuration
  async getModels(): Promise<any[]> {
    try {
      const response = await this.client.get('/config/models')
      return response.data
    } catch (error) {
      console.error('Failed to fetch models:', error)
      // Return default models if API fails
      return [
        { id: 'gpt-4', name: 'GPT-4', contextWindow: 8192 },
        { id: 'gpt-3.5-turbo', name: 'GPT-3.5 Turbo', contextWindow: 4096 },
      ]
    }
  }

  async getAvailableModels(): Promise<string[]> {
    const response = await this.client.get('/config/models')
    return response.data
  }

  async getSearchTools(): Promise<{ id: string; name: string }[]> {
    try {
      const response = await this.client.get('/config/search-tools')
      return response.data
    } catch (error) {
      console.error('Failed to fetch search tools:', error)
      // Return default search tools if API fails
      return [
        { id: 'searxng', name: 'SearXNG' },
        { id: 'google', name: 'Google' },
        { id: 'bing', name: 'Bing' },
        { id: 'duckduckgo', name: 'DuckDuckGo' },
      ]
    }
  }

  async saveConfig(config: ResearchConfig): Promise<void> {
    await this.client.post('/config/save', config)
  }

  async updateSessionConfig(sessionId: string, config: any): Promise<void> {
    await this.client.put(`/chat/sessions/${sessionId}/config`, config)
  }
}

export const apiService = new ApiService()
