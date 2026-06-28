# Production API image — build from repository root:
#   docker build -f backend/Dockerfile -t runpod-orchestrator:latest .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/RunpodOrchestrator.Domain/RunpodOrchestrator.Domain.csproj backend/RunpodOrchestrator.Domain/
COPY backend/RunpodOrchestrator.Application/RunpodOrchestrator.Application.csproj backend/RunpodOrchestrator.Application/
COPY backend/RunpodOrchestrator.Infrastructure/RunpodOrchestrator.Infrastructure.csproj backend/RunpodOrchestrator.Infrastructure/
COPY backend/RunpodOrchestrator.API/RunpodOrchestrator.API.csproj backend/RunpodOrchestrator.API/

RUN dotnet restore backend/RunpodOrchestrator.API/RunpodOrchestrator.API.csproj

COPY backend/ .

WORKDIR /src
RUN dotnet publish RunpodOrchestrator.API/RunpodOrchestrator.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --uid 10001 --create-home --shell /bin/bash appuser

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

RUN chown -R appuser:appuser /app

USER appuser

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "RunpodOrchestrator.API.dll"]
