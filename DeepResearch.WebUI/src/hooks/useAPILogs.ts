/**
 * Hook for using the API log store in React components
 * Subscribes to log changes and re-renders on updates
 */
import { useEffect, useState } from 'react'
import { apiLogStore, type APILog } from '@stores/apiLogStore'

export const useAPILogs = () => {
  const [logs, setLogs] = useState<APILog[]>([])

  useEffect(() => {
    // Subscribe to log changes
    const unsubscribe = apiLogStore.onChange((newLogs) => {
      setLogs(newLogs)
    })

    // Get initial logs
    setLogs(apiLogStore.getLogs())

    // Cleanup on unmount
    return () => {
      unsubscribe()
    }
  }, [])

  return {
    logs,
    addLog: (log: APILog) => apiLogStore.addLog(log),
    addInfoLog: (category: string, message: string, icon?: string) =>
      apiLogStore.addInfoLog(category, message, icon),
    addWarningLog: (category: string, message: string, icon?: string) =>
      apiLogStore.addWarningLog(category, message, icon),
    addErrorLog: (category: string, message: string, icon?: string) =>
      apiLogStore.addErrorLog(category, message, icon),
    clear: () => apiLogStore.clear(),
    getLogs: () => apiLogStore.getLogs()
  }
}
