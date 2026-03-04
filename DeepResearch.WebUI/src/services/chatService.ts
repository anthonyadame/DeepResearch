// src/services/chatService.ts
// Step-by-step chat endpoint wrapper

import type { ChatStepRequest, ChatStepResponse } from '../types/index'

// ✅ CORRECT PORT: 5000 (where API actually runs)
const API_BASE = 'http://localhost:5000/api/chat'

export async function executeStep(
  request: ChatStepRequest,
  signal?: AbortSignal
): Promise<ChatStepResponse> {
  try {
    console.log('[ChatService] Posting to:', `${API_BASE}/step`)
    console.log('[ChatService] Request:', request)

    const response = await fetch(`${API_BASE}/step`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(request),
      signal,
    })

    console.log('[ChatService] Response status:', response.status)

    if (!response.ok) {
      const error = await response.json().catch(() => ({}))
      throw new Error(error.details || `HTTP ${response.status}: Failed to execute step`)
    }

    const data: ChatStepResponse = await response.json()
    console.log('[ChatService] Response data:', data)
    return data
  } catch (error) {
    const errorMessage = error instanceof Error ? error.message : 'Failed to execute step'
    console.error('[ChatService] Error:', errorMessage, error)
    throw error
  }
}

export async function healthCheck(): Promise<boolean> {
  try {
    const response = await fetch(`${API_BASE.replace('/chat', '')}/health`)
    return response.ok
  } catch {
    return false
  }
}
