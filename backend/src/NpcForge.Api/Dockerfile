# Contexto de build: pasta backend/ (veja docker-compose.yml na raiz).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY NpcForge.slnx ./
COPY src/NpcForge.Core/NpcForge.Core.csproj src/NpcForge.Core/
COPY src/NpcForge.Infrastructure/NpcForge.Infrastructure.csproj src/NpcForge.Infrastructure/
COPY src/NpcForge.Api/NpcForge.Api.csproj src/NpcForge.Api/
RUN dotnet restore src/NpcForge.Api/NpcForge.Api.csproj

COPY src/ src/
RUN dotnet publish src/NpcForge.Api/NpcForge.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "NpcForge.Api.dll"]
