/**
 * Stream State Formatter - Helper functions for displaying StreamState updates
 * Mirrors the C# StreamStateFormatter from DeepResearchAgent.Services
 */

import type { StreamState, ResearchProgress } from '@types/index'

/**
 * Formats a single StreamState field for display
 * @param label - Field label/name
 * @param value - Field value
 * @returns Formatted string or empty if value is empty
 */
export function formatStreamStateField(label: string, value?: string): string {
  if (!value || value.trim() === '') {
    return ''
  }
  return `${label}: ${value}`
}

/**
 * Filters and returns all populated fields from a StreamState object
 * @param state - The StreamState object
 * @returns Array of formatted field strings
 */
export function getStreamStateFields(state: StreamState): Array<{ label: string; value: string }> {
  const fields: Array<{ label: string; value: string }> = []

  const fieldMappings = [
    { key: 'status', label: 'Status' },
    { key: 'researchId', label: 'Research ID' },
    { key: 'userQuery', label: 'Query' },
    { key: 'briefPreview', label: 'Brief Preview' },
    { key: 'researchBrief', label: 'Research Brief' },
    { key: 'draftReport', label: 'Draft Report' },
    { key: 'refinedSummary', label: 'Refined Summary' },
    { key: 'finalReport', label: 'Final Report' },
    { key: 'supervisorUpdate', label: 'Supervisor Update' },
  ]

  fieldMappings.forEach(({ key, label }) => {
    const value = state[key as keyof StreamState]
    if (value && typeof value === 'string' && value.trim() !== '') {
      fields.push({ label, value })
    }
  })

  if (state.supervisorUpdateCount && state.supervisorUpdateCount > 0) {
    fields.push({ label: 'Supervisor Updates', value: state.supervisorUpdateCount.toString() })
  }

  return fields
}

/**
 * Gets a human-readable progress summary from StreamState
 * Useful for status bars and progress indicators
 * @param state - The StreamState object
 * @returns Progress summary string
 */
export function getProgressSummary(state: StreamState): string {
  const parts: string[] = []

  if (state.status) {
    const statusMatch = state.status.match(/"status"\s*:\s*"([^"]+)"/)
    if (statusMatch) {
      parts.push(`📊 ${statusMatch[1]}`)
    }
  }

  if (state.researchBrief) {
    parts.push('✓ Research Brief')
  }

  if (state.draftReport) {
    parts.push('✓ Draft Report')
  }

  if (state.supervisorUpdateCount && state.supervisorUpdateCount > 0) {
    parts.push(`✓ Supervisor (${state.supervisorUpdateCount})`)
  }

  if (state.refinedSummary) {
    parts.push('✓ Refined Summary')
  }

  if (state.finalReport) {
    parts.push('✓ Final Report')
  }

  return parts.length > 0 ? parts.join(' | ') : 'Initializing...'
}

/**
 * Extracts the primary/most recent content from StreamState
 * Returns content in order of priority: Final > Refined > Draft > Brief > Preview > Update
 * @param state - The StreamState object
 * @returns The most relevant content string
 */
export function getPhaseContent(state: StreamState): string {
  if (state.finalReport) {
    return state.finalReport
  }

  if (state.refinedSummary) {
    return state.refinedSummary
  }

  if (state.draftReport) {
    return state.draftReport
  }

  if (state.researchBrief) {
    return state.researchBrief
  }

  if (state.briefPreview) {
    return state.briefPreview
  }

  if (state.supervisorUpdate) {
    return state.supervisorUpdate
  }

  return state.status || ''
}

/**
 * Determines current research phase based on StreamState content
 * @param state - The StreamState object
 * @returns Current phase identifier
 */
export function getCurrentPhase(state: StreamState): ResearchProgress['phase'] {
  if (state.finalReport) {
    return 'final'
  }

  if (state.supervisorUpdateCount && state.supervisorUpdateCount > 0) {
    return 'supervisor'
  }

  if (state.draftReport) {
    return 'draft'
  }

  if (state.researchBrief) {
    return 'brief'
  }

  if (state.status?.includes('clarif')) {
    return 'clarify'
  }

  return 'clarify'
}

/**
 * Calculates research progress percentage (0-100)
 * @param state - The StreamState object
 * @returns Progress percentage
 */
export function calculateProgress(state: StreamState): number {
  // Define progress checkpoints
  const checkpoints = [
    { phase: 'clarify', percent: 5 },
    { phase: 'brief', percent: 20 },
    { phase: 'draft', percent: 40 },
    { phase: 'supervisor', percent: 80 },
    { phase: 'final', percent: 100 },
  ]

  const currentPhase = getCurrentPhase(state)

  // If supervisor phase, estimate based on update count
  if (currentPhase === 'supervisor' && state.supervisorUpdateCount) {
    const supervisorPercent = Math.min(40 + (state.supervisorUpdateCount * 2), 95)
    return supervisorPercent
  }

  const checkpoint = checkpoints.find((cp) => cp.phase === currentPhase)
  return checkpoint?.percent || 0
}

/**
 * Generates a human-readable message for current state
 * @param state - The StreamState object
 * @returns Progress message
 */
export function getProgressMessage(state: StreamState): string {
  const phase = getCurrentPhase(state)

  const messages: Record<string, string> = {
    clarify: '🔍 Clarifying your research query...',
    brief: '📝 Writing research brief...',
    draft: '📄 Generating initial draft...',
    supervisor: `🔄 Refining report (${state.supervisorUpdateCount || 0} updates)...`,
    final: '✨ Generating final report...',
    complete: '✅ Research complete!',
    error: '❌ An error occurred',
  }

  return messages[phase] || 'Processing...'
}

/**
 * Converts StreamState to a ResearchProgress object for UI components
 * @param state - The StreamState object
 * @returns ResearchProgress object
 */
export function streamStateToProgress(state: StreamState): ResearchProgress {
  return {
    phase: getCurrentPhase(state),
    percentage: calculateProgress(state),
    message: getProgressMessage(state),
    content: getPhaseContent(state),
  }
}

/**
 * Truncates content to a maximum length with ellipsis
 * @param content - The content to truncate
 * @param maxLength - Maximum length (default: 200)
 * @returns Truncated content
 */
export function truncateContent(content: string, maxLength: number = 200): string {
  if (content.length <= maxLength) {
    return content
  }
  return content.substring(0, maxLength) + '...'
}

/**
 * Extracts plain text from status JSON string
 * @param statusJson - JSON status string from StreamState
 * @returns Extracted status text or original if not JSON
 */
export function parseStatusJson(statusJson?: string): string {
  if (!statusJson) return 'Processing...'

  try {
    // Try to extract status field from JSON
    const match = statusJson.match(/"status"\s*:\s*"([^"]+)"/)
    if (match) {
      return match[1]
    }

    // Try to parse as JSON and look for message field
    const parsed = JSON.parse(statusJson)
    return parsed.message || parsed.status || statusJson
  } catch {
    // Not JSON, return as-is
    return statusJson
  }
}
