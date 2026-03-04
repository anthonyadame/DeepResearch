/**
 * Streaming API Client for MasterWorkflow endpoint
 * Handles Server-Sent Events (SSE) stream parsing and StreamState consumption
 */

import type { StreamState, ResearchConfig } from '@types/index'

interface StreamOptions {
  onStateReceived: (state: StreamState) => void
  onError?: (error: Error) => void
  onComplete?: () => void
  timeout?: number
}

/**
 * Client for consuming MasterWorkflow streams from the API
 */
export class MasterWorkflowStreamClient {
  private baseURL: string
  private abortController: AbortController | null = null

  constructor(baseURL: string = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api') {
    this.baseURL = baseURL
  }

  /**
   * Stream the MasterWorkflow with real-time progress updates
   * Handles Server-Sent Events parsing and StreamState deserialization
   *
   * @param userQuery - Research query from user
   * @param options - Stream options with callbacks
   * @returns Promise that resolves when stream completes
   */
  async streamMasterWorkflow(userQuery: string, options: StreamOptions): Promise<void> {
    if (!userQuery || !userQuery.trim()) {
      const error = new Error('User query cannot be empty')
      options.onError?.(error)
      throw error
    }

    this.abortController = new AbortController()
    const timeoutId = options.timeout ? setTimeout(() => this.abortController?.abort(), options.timeout) : null

    try {
      const url = `${this.baseURL}/workflows/master/stream`
      const payload = { userQuery }

      const response = await fetch(url, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(payload),
        signal: this.abortController.signal,
      })

      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`)
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
            options.onComplete?.()
            break
          }

          buffer += decoder.decode(value, { stream: true })
          const lines = buffer.split('\n')

          // Keep the last line in buffer if it's incomplete
          buffer = lines.pop() || ''

          for (const line of lines) {
            if (line.startsWith('data: ')) {
              const jsonStr = line.substring(6).trim()

              if (jsonStr === '[DONE]' || jsonStr === '{"status":"completed"}') {
                options.onComplete?.()
                return
              }

              if (jsonStr && jsonStr !== '') {
                try {
                  const state: StreamState = JSON.parse(jsonStr)
                  options.onStateReceived(state)
                } catch (parseError) {
                  console.error('Failed to parse StreamState:', jsonStr, parseError)
                }
              }
            }
          }
        }
      } finally {
        reader.releaseLock()
      }
    } catch (error) {
      if (error instanceof Error) {
        if (error.name === 'AbortError') {
          console.log('Stream aborted')
        } else {
          options.onError?.(error)
          throw error
        }
      } else {
        const err = new Error('Unknown streaming error')
        options.onError?.(err)
        throw err
      }
    } finally {
      if (timeoutId) {
        clearTimeout(timeoutId)
      }
    }
  }

  /**
   * Collect all StreamState objects from a stream into an array
   * Useful for testing or when you need all results at once
   *
   * @param userQuery - Research query from user
   * @returns Array of all StreamState objects
   */
  async collectStream(userQuery: string): Promise<StreamState[]> {
    const states: StreamState[] = []

    return new Promise((resolve, reject) => {
      this.streamMasterWorkflow(userQuery, {
        onStateReceived: (state) => states.push(state),
        onError: reject,
        onComplete: () => resolve(states),
      }).catch(reject)
    })
  }

  /**
   * Get the final report from a stream
   * Useful when you only care about the end result
   *
   * @param userQuery - Research query from user
   * @returns Final report string or null if not completed
   */
  async getFinallReport(userQuery: string): Promise<string | null> {
    const states = await this.collectStream(userQuery)
    const finalState = states.find((s) => s.finalReport)
    return finalState?.finalReport || null
  }

  /**
   * Cancel the current stream
   */
  cancel(): void {
    if (this.abortController) {
      this.abortController.abort()
      this.abortController = null
    }
  }

  /**
   * Check if a stream is currently active
   */
  isStreaming(): boolean {
    return this.abortController !== null && !this.abortController.signal.aborted
  }
}

/**
 * Singleton instance for convenient access
 */
let clientInstance: MasterWorkflowStreamClient | null = null

/**
 * Get or create the MasterWorkflowStreamClient singleton
 */
export function getMasterWorkflowStreamClient(
  baseURL?: string
): MasterWorkflowStreamClient {
  if (!clientInstance) {
    clientInstance = new MasterWorkflowStreamClient(baseURL)
  }
  return clientInstance
}
