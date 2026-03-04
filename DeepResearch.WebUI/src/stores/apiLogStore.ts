export interface APILog {
  timestamp: string
  level: 'info' | 'warning' | 'error' | 'debug'
  category: string
  message: string
  icon: string
}

class APILogStore {
  private logs: APILog[] = []
  private maxLogs = 100
  private listeners: Set<(logs: APILog[]) => void> = new Set()

  addLog(log: APILog) {
    this.logs.unshift(log)
    if (this.logs.length > this.maxLogs) {
      this.logs.pop()
    }
    this.notifyListeners()
  }

  addInfoLog(category: string, message: string, icon: string = 'ℹ️') {
    this.addLog({
      timestamp: new Date().toLocaleTimeString(),
      level: 'info',
      category,
      message,
      icon
    })
  }

  addWarningLog(category: string, message: string, icon: string = '⚠️') {
    this.addLog({
      timestamp: new Date().toLocaleTimeString(),
      level: 'warning',
      category,
      message,
      icon
    })
  }

  addErrorLog(category: string, message: string, icon: string = '❌') {
    this.addLog({
      timestamp: new Date().toLocaleTimeString(),
      level: 'error',
      category,
      message,
      icon
    })
  }

  clear() {
    this.logs = []
    this.notifyListeners()
  }

  getLogs() {
    return this.logs
  }

  private notifyListeners() {
    this.listeners.forEach(listener => listener(this.logs))
  }

  // React hook: subscribe to log changes
  onChange(listener: (logs: APILog[]) => void): () => void {
    this.listeners.add(listener)
    // Return unsubscribe function
    return () => {
      this.listeners.delete(listener)
    }
  }
}

export const apiLogStore = new APILogStore()
