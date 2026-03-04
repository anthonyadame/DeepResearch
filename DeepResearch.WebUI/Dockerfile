# Build stage
FROM node:20-alpine AS builder

WORKDIR /app

# Install build dependencies for native modules
RUN apk add --no-cache python3 make g++

# Copy package files
COPY DeepResearchAgent.UI/package*.json ./

# Install dependencies with verbose output and timeout settings
RUN npm install --loglevel=verbose --fetch-timeout=60000

# Copy source code
COPY DeepResearchAgent.UI/src ./src
COPY DeepResearchAgent.UI/*.ts* ./
COPY DeepResearchAgent.UI/*.js ./
COPY DeepResearchAgent.UI/index.html ./

# Build application
RUN npm run build

# Production stage
FROM node:20-alpine

WORKDIR /app

# Install serve to run the app
RUN npm install -g serve

# Copy built application from builder
COPY --from=builder /app/dist ./dist

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD wget --quiet --tries=1 --spider http://localhost:5173/index.html || exit 1

# Expose port
EXPOSE 5173

# Start application
CMD ["serve", "-s", "dist", "-l", "5173"]
