# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS builder

ARG BUILD_CONFIGURATION=Release

WORKDIR /src

# Copy project files
COPY ["DeepResearchAgent/DeepResearchAgent.csproj", "DeepResearchAgent/"]
COPY ["DeepResearch.Api/DeepResearch.Api.csproj", "DeepResearch.Api/"]

# Restore dependencies
RUN dotnet restore "DeepResearch.Api/DeepResearch.Api.csproj"

# Copy source code
COPY . .

# Build the API project
WORKDIR "/src/DeepResearch.Api"
RUN dotnet build "DeepResearch.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage
FROM builder AS publish

ARG BUILD_CONFIGURATION=Release

RUN dotnet publish "DeepResearch.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base

WORKDIR /app

# Create non-root user for security
RUN useradd -m -u 1000 appuser && \
    mkdir -p /app/data/checkpoints /app/data/keys && \
    chown -R appuser:appuser /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Expose ports
EXPOSE 5000

# Copy published application
COPY --from=publish --chown=appuser:appuser /app/publish .

# Switch to non-root user
USER appuser

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

# Entry point
ENTRYPOINT ["dotnet", "DeepResearch.Api.dll"]
