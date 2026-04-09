FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/core/Assimalign.AI.Orchestrator.Core/Assimalign.AI.Orchestrator.Core.csproj backend/core/Assimalign.AI.Orchestrator.Core/
COPY backend/application/Assimalign.AI.Orchestrator.Application/Assimalign.AI.Orchestrator.Application.csproj backend/application/Assimalign.AI.Orchestrator.Application/
COPY backend/infrastructure/Assimalign.AI.Orchestrator.Infrastructure/Assimalign.AI.Orchestrator.Infrastructure.csproj backend/infrastructure/Assimalign.AI.Orchestrator.Infrastructure/
COPY backend/infrastructure/Assimalign.AI.Orchestrator.Infrastructure.Messaging/Assimalign.AI.Orchestrator.Infrastructure.Messaging.csproj backend/infrastructure/Assimalign.AI.Orchestrator.Infrastructure.Messaging/
COPY backend/infrastructure/Assimalign.AI.Orchestrator.Infrastructure.Messaging.ServiceBus/Assimalign.AI.Orchestrator.Infrastructure.Messaging.ServiceBus.csproj backend/infrastructure/Assimalign.AI.Orchestrator.Infrastructure.Messaging.ServiceBus/
COPY backend/infrastructure/Assimalign.AI.Orchestrator.Infrastructure.Storage/Assimalign.AI.Orchestrator.Infrastructure.Storage.csproj backend/infrastructure/Assimalign.AI.Orchestrator.Infrastructure.Storage/
COPY backend/infrastructure/Assimalign.AI.Orchestrator.Infrastructure.Storage.Memory/Assimalign.AI.Orchestrator.Infrastructure.Storage.Memory.csproj backend/infrastructure/Assimalign.AI.Orchestrator.Infrastructure.Storage.Memory/
COPY backend/infrastructure/Assimalign.AI.Orchestrator.Infrastructure.Storage.Tables/Assimalign.AI.Orchestrator.Infrastructure.Storage.Tables.csproj backend/infrastructure/Assimalign.AI.Orchestrator.Infrastructure.Storage.Tables/
COPY backend/services/Assimalign.AI.Orchestrator.Api/Assimalign.AI.Orchestrator.Api.csproj backend/services/Assimalign.AI.Orchestrator.Api/

RUN dotnet restore backend/services/Assimalign.AI.Orchestrator.Api/Assimalign.AI.Orchestrator.Api.csproj

COPY . .
RUN dotnet publish backend/services/Assimalign.AI.Orchestrator.Api/Assimalign.AI.Orchestrator.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runner
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "Assimalign.AI.Orchestrator.Api.dll"]
