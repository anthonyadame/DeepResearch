// services/chatServiceDebug.ts
// Debug version with logging to help diagnose issues

import type { ChatStepRequest, ChatStepResponse } from '../types';

const API_BASE = 'http://localhost:5000/api/chat';

// Debug logging
const DEBUG = true;

function log(message: string, data?: any) {
  if (DEBUG) {
    console.log(`[ChatService] ${message}`, data || '');
  }
}

function error(message: string, err?: any) {
  console.error(`[ChatService] ❌ ${message}`, err || '');
}

export async function executeStep(
  request: ChatStepRequest,
  signal?: AbortSignal
): Promise<ChatStepResponse> {
  const url = `${API_BASE}/step`;
  
  log(`📤 Executing step...`);
  log(`URL: ${url}`);
  log(`Request:`, request);

  try {
    const response = await fetch(url, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(request),
      signal,
    });

    log(`Response status: ${response.status}`);

    if (!response.ok) {
      let errorDetails = `HTTP ${response.status}`;
      
      try {
        const errorData = await response.json();
        errorDetails = errorData.details || errorData.error || errorDetails;
        log(`Error response:`, errorData);
      } catch (e) {
        const text = await response.text();
        log(`Error text:`, text);
      }

      const errorMessage = `Failed to execute step: ${errorDetails}`;
      error(errorMessage);
      throw new Error(errorMessage);
    }

    const data: ChatStepResponse = await response.json();
    log(`✅ Step executed successfully`, data);
    return data;

  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    
    if (message.includes('Failed to fetch')) {
      error(`Network error - API not reachable at ${API_BASE}. Is the backend running?`, err);
      throw new Error(
        `Unable to connect to API at ${API_BASE}. Make sure the backend is running with: dotnet run --project DeepResearchAgent.Api`
      );
    }
    
    error(`Request failed: ${message}`, err);
    throw err;
  }
}

export async function createSession(title?: string): Promise<any> {
  const url = `${API_BASE}/sessions`;
  
  log(`📤 Creating session...`);
  log(`URL: ${url}`);

  try {
    const response = await fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ title }),
    });

    if (!response.ok) {
      throw new Error(`Failed to create session: HTTP ${response.status}`);
    }

    const data = await response.json();
    log(`✅ Session created`, data);
    return data;

  } catch (err) {
    error(`Session creation failed`, err);
    throw err;
  }
}

export async function healthCheck(): Promise<boolean> {
  try {
    log(`🏥 Checking API health...`);
    const response = await fetch(`http://localhost:5000/health`, {
      method: 'GET',
    });
    const isHealthy = response.ok;
    log(`API health: ${isHealthy ? '✅ Healthy' : '❌ Not healthy'}`, response.status);
    return isHealthy;
  } catch (err) {
    error(`Health check failed - API not reachable`, err);
    return false;
  }
}
