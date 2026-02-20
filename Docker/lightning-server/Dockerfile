FROM node:20-slim as dashboard-builder

WORKDIR /build

RUN apt-get update && apt-get install -y git && rm -rf /var/lib/apt/lists/*

RUN git clone --depth 1 --branch main https://github.com/microsoft/agent-lightning.git

WORKDIR /build/agent-lightning/dashboard

RUN npm ci
RUN node - <<'NODE'
const fs = require('fs');
const p = 'src/Router.tsx';
let s = fs.readFileSync(p, 'utf8');
if (!s.includes('basename')) {
  s = s.replace(/createBrowserRouter\(([^)]*)\)/,
    'createBrowserRouter($1, { basename: import.meta.env.BASE_URL || "/dashboard" })');
  fs.writeFileSync(p, s);
  console.log('Injected basename "/dashboard" into router');
} else {
  console.log('Router basename already present');
}
NODE

RUN npm run build -- --base=/dashboard/

# ========== CPU-only Python runtime ==========
FROM python:3.11-slim as base

# Set environment variables
ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    PIP_NO_CACHE_DIR=1 \
    PIP_DISABLE_PIP_VERSION_CHECK=1

WORKDIR /app

# Install system dependencies
RUN apt-get update && apt-get install -y \
    curl \
    gcc \
    g++ \
    git \
    build-essential \
    && rm -rf /var/lib/apt/lists/*

# Copy requirements
COPY Docker/lightning-server/requirements.txt .

# Install PyTorch CPU version first
RUN pip install --no-cache-dir \
    torch==2.4.0 \
    torchvision==0.19.0 \
    --index-url https://download.pytorch.org/whl/cpu

# Install Agent Lightning with APO support
RUN pip install --no-cache-dir --upgrade agentlightning
RUN pip install --no-cache-dir agentlightning[apo]

# Install remaining dependencies from requirements.txt
RUN pip install --no-cache-dir \
    fastapi==0.109.0 \
    uvicorn[standard]==0.27.0 \
    pydantic==2.5.3 \
    pydantic-settings==2.1.0 \
    httpx==0.26.0 \
    openai>=2.0.0 \
    python-json-logger==2.0.7 \
    prometheus-client==0.19.0 \
    psutil==5.9.8 \
    numpy==1.26.3 \
    nltk==3.8.1 \
    textblob==0.17.1

# Copy built dashboard assets from the package output path
COPY --from=dashboard-builder /build/agent-lightning/agentlightning/dashboard /app/agentlightning/dashboard

# Copy application code
COPY Docker/lightning-server/server.py .

# Download NLTK data (required for textblob)
RUN python -c "import nltk; nltk.download('punkt'); nltk.download('brown')"

# Create non-root user and fix permissions for litellm UI path
RUN useradd -m -u 1000 lightning && \
    mkdir -p /usr/local/lib/python3.11/dist-packages/litellm/proxy/_experimental/out && \
    chown -R lightning:lightning /usr/local/lib/python3.11/dist-packages/litellm && \
    chown -R lightning:lightning /app

USER lightning

# Expose port
EXPOSE 9090

# Health check
HEALTHCHECK --interval=30s --timeout=10s --retries=3 \
    CMD curl -f http://localhost:8090/health || exit 1

# Run the server
CMD ["python", "server.py"]

# ========== CUDA 12.4-enabled variant for VERL with Dashboard ==========
FROM nvidia/cuda:12.4.0-devel-ubuntu22.04 as cuda

ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    PIP_NO_CACHE_DIR=1 \
    PIP_DISABLE_PIP_VERSION_CHECK=1 \
    CUDA_VISIBLE_DEVICES=0

WORKDIR /app

# Install Python 3.11, Node.js, and build tools
RUN apt-get update && apt-get install -y \
    python3.11 \
    python3.11-dev \
    python3-pip \
    curl \
    git \
    build-essential \
    ninja-build \
    && rm -rf /var/lib/apt/lists/*

# Create Python symlink
RUN ln -sf /usr/bin/python3.11 /usr/bin/python && \
    ln -sf /usr/bin/python3.11 /usr/bin/python3

# Upgrade pip
RUN python -m pip install --upgrade pip setuptools wheel

# Install PyTorch with CUDA 12.4 support
RUN pip install --no-cache-dir \
    torch==2.4.0 \
    torchvision==0.19.0 \
    --index-url https://download.pytorch.org/whl/cu124

# flash-attn build-time deps
RUN pip install --no-cache-dir psutil packaging

# Install flash-attention (required for VERL)
RUN pip install --no-cache-dir flash-attn --no-build-isolation

# Install vLLM (required for VERL)
RUN pip install --no-cache-dir vllm==0.6.3.post1

# Install Agent Lightning with APO and VERL support
RUN pip install --no-cache-dir --upgrade agentlightning
RUN pip install --no-cache-dir agentlightning[apo]

# Manual VERL dependencies
RUN pip install --no-cache-dir \
    verl==0.5.0 \
    fastapi==0.109.0 \
    uvicorn[standard]==0.27.0 \
    pydantic==2.5.3 \
    pydantic-settings==2.1.0 \
    httpx==0.26.0 \
    openai>=2.0.0 \
    python-json-logger==2.0.7 \
    prometheus-client==0.19.0 \
    psutil==5.9.8 \
    numpy==1.26.3 \
    nltk==3.8.1 \
    textblob==0.17.1

# Copy built dashboard assets from the package output path
COPY --from=dashboard-builder /build/agent-lightning/agentlightning/dashboard /app/agentlightning/dashboard

# Copy application code
COPY Docker/lightning-server/server.py .

# Download NLTK data
RUN python -c "import nltk; nltk.download('punkt'); nltk.download('brown')"

# Create non-root user and fix permissions for litellm UI path
RUN useradd -m -u 1000 lightning && \
    mkdir -p /usr/local/lib/python3.11/dist-packages/litellm/proxy/_experimental/out && \
    chown -R lightning:lightning /usr/local/lib/python3.11/dist-packages/litellm && \
    chown -R lightning:lightning /app

USER lightning

EXPOSE 9090

HEALTHCHECK --interval=30s --timeout=10s --retries=3 \
    CMD curl -f http://localhost:8090/health || exit 1

CMD ["python", "server.py"]

# Build commands:
# docker build --no-cache -t lightning-server:cuda --target cuda .
# or for CPU
# docker build --no-cache -t lightning-server:latest --target base .