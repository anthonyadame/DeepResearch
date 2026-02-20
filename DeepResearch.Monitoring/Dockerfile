# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS builder

ARG BUILD_CONFIGURATION=Release

WORKDIR /src

# Copy project files
COPY ["DeepResearchAgent/DeepResearchAgent.csproj", "DeepResearchAgent/"]
COPY ["DeepResearch.Api/DeepResearch.Api.csproj", "DeepResearch.Api/"]
COPY ["DeepResearch.Monitoring/DeepResearch.Monitoring.csproj", "DeepResearch.Monitoring/"]

# Restore dependencies
RUN dotnet restore "DeepResearch.Monitoring/DeepResearch.Monitoring.csproj"

# Copy source code
COPY . .

# Build the monitoring project
WORKDIR "/src/DeepResearch.Monitoring"
RUN dotnet build "DeepResearch.Monitoring.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage
FROM builder AS publish

ARG BUILD_CONFIGURATION=Release

RUN dotnet publish "DeepResearch.Monitoring.csproj" -c $BUILD_CONFIGURATION -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base

WORKDIR /app

# Create non-root user for security
RUN useradd -m -u 1000 appuser && \
    mkdir -p /app/data && \
    chown -R appuser:appuser /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Expose ports
EXPOSE 8081

# Copy published application
COPY --from=publish --chown=appuser:appuser /app/publish .

# Switch to non-root user
USER appuser

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8081/health || exit 1

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8081
ENV ASPNETCORE_ENVIRONMENT=Production

# Entry point
ENTRYPOINT ["dotnet", "DeepResearch.Monitoring.dll"]
