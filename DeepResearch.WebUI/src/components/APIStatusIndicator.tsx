// components/APIStatusIndicator.tsx
import { useState, useEffect } from 'react';
import { healthCheck } from '../services/chatServiceDebug';

export function APIStatusIndicator() {
  const [status, setStatus] = useState<'checking' | 'connected' | 'disconnected'>('checking');
  const [message, setMessage] = useState('Checking API connection...');

  useEffect(() => {
    const checkConnection = async () => {
      try {
        console.log('[StatusIndicator] Checking API connection...');
        const isHealthy = await healthCheck();
        
        if (isHealthy) {
          setStatus('connected');
          setMessage('✅ Connected to http://localhost:5000');
          console.log('[StatusIndicator] Connected!');
        } else {
          setStatus('disconnected');
          setMessage('❌ API returned unhealthy status');
          console.log('[StatusIndicator] API unhealthy');
        }
      } catch (error) {
        setStatus('disconnected');
        const message = error instanceof Error ? error.message : 'Unknown error';
        setMessage(`❌ Cannot reach API: ${message}`);
        console.error('[StatusIndicator] Connection failed:', error);
      }
    };

    checkConnection();
    
    // Check every 5 seconds
    const interval = setInterval(checkConnection, 5000);
    return () => clearInterval(interval);
  }, []);

  return (
    <div className="api-status-indicator">
      <div className={`status-badge status-${status}`}>
        <span className="status-dot"></span>
        <span className="status-text">{message}</span>
      </div>
    </div>
  );
}
