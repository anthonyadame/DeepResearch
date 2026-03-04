# DeepResearch WebUI

Modern React + TypeScript web interface for the DeepResearch API (PhoenixAI Platform).

## 🚀 Quick Start

### Prerequisites

- Node.js 18+ and npm
- DeepResearch.Api running (default: http://localhost:5000)

### Installation

```bash
# Install dependencies
npm install

# Create environment configuration
cp .env.example .env.local

# Update .env.local with your API URL if different from default
# VITE_API_BASE_URL=http://localhost:5000/api
```

### Development

```bash
# Start development server
npm run dev

# Open in browser
http://localhost:5173
```

### Build

```bash
# Build for production
npm run build

# Preview production build
npm run preview
```

## 🔌 API Integration

The WebUI communicates with the DeepResearch API through the `apiService` in `src/services/api.ts`.

**Default API URL:** `http://localhost:5000/api`

Configure via environment variable: `VITE_API_BASE_URL` in `.env.local`

### API Endpoints Used

#### Chat Endpoints
- `POST /chat/sessions` - Create new chat session
- `GET /chat/sessions` - List all sessions
- `GET /chat/sessions/{id}` - Get specific session
- `DELETE /chat/sessions/{id}` - Delete session
- `POST /chat/sessions/{id}/query` - Submit research query
- `POST /chat/sessions/{id}/stream` - Stream query response (SSE)
- `GET /chat/sessions/{id}/history` - Get chat history
- `PUT /chat/sessions/{id}/config` - Update session config

#### Workflow Endpoints
- `POST /workflows/master/stream` - Stream MasterWorkflow execution (SSE)

#### Configuration Endpoints
- `GET /config/models` - List available LLM models
- `GET /config/search-tools` - List search tools
- `POST /config/save` - Save configuration

## 📁 Project Structure

```
src/
├── components/          # React components
│   ├── ChatDialog.tsx   # Main chat interface
│   ├── Sidebar.tsx      # Navigation sidebar
│   ├── InputBar.tsx     # Message input
│   ├── MessageList.tsx  # Chat messages
│   ├── ResearchStreamingPanel.tsx  # Workflow streaming display
│   └── ...
├── services/            # API communication
│   ├── api.ts           # Main API client
│   └── masterWorkflowStreamClient.ts  # Workflow streaming
├── hooks/               # Custom React hooks
│   ├── useChat.ts       # Chat state management
│   └── useMasterWorkflowStream.ts  # Workflow streaming hook
├── types/               # TypeScript types
│   └── index.ts         # Type definitions
├── stores/              # Zustand state stores
│   ├── debugStore.ts
│   └── sessionConfigStore.ts
├── App.tsx              # Root component
├── main.tsx             # Entry point
└── index.css            # Global styles
```

## 🎨 Features

### Chat Interface
- Create and manage research sessions
- Send queries and receive responses
- Stream workflow execution in real-time
- View research progress through all 5 phases

### Research Streaming
- Real-time progress updates via Server-Sent Events
- Display of:
  - Brief Preview
  - Research Brief
  - Draft Report
  - Refined Summary
  - Final Report

### Configuration
- Select LLM models
- Choose search tools
- Configure research parameters

## 🛠️ Development

### Environment Variables

Create `.env.local` file:

```bash
# API Configuration
VITE_API_BASE_URL=http://localhost:5000/api

# App Configuration
VITE_APP_NAME=DeepResearch WebUI
VITE_LOG_LEVEL=info
```

### Building

```bash
# Development build
npm run dev

# Production build
npm run build

# Type checking
npm run type-check

# Linting
npm run lint
```

## 🚀 Deployment

### Docker

```bash
# Build Docker image
docker build -t deepresearch-webui .

# Run container
docker run -p 5173:5173 -e VITE_API_BASE_URL=http://your-api:5000/api deepresearch-webui
```

### Static Hosting

```bash
# Build for production
npm run build

# Deploy the 'dist' folder to your hosting provider
# (Netlify, Vercel, AWS S3, etc.)
```

## 📝 Notes

- This WebUI is designed to work with the DeepResearch.Api project
- API endpoints do not require authentication for easier integration
- Chat sessions are stored in-memory on the API side (not persisted)
- Workflow streaming uses Server-Sent Events (SSE)

## 🔗 Related Projects

- **DeepResearch.Api** - Backend API for workflow orchestration
- **DeepResearchAgent** - Core agent and workflow library
