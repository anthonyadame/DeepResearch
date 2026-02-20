FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY DeepResearchAgent/DeepResearchAgent.csproj ./DeepResearchAgent/
RUN dotnet restore ./DeepResearchAgent/DeepResearchAgent.csproj

COPY DeepResearchAgent/. ./DeepResearchAgent/
WORKDIR /src/DeepResearchAgent
RUN dotnet publish -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
ENV LIGHTNING_SERVER_URL=http://localhost:8090
COPY --from=build /app/out ./
ENTRYPOINT ["dotnet", "DeepResearchAgent.dll"]
