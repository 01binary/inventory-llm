FROM node:20-bookworm-slim AS client-build
WORKDIR /src/app/ClientApp

COPY app/ClientApp/package*.json ./
RUN npm install

COPY app/ClientApp/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS server-build
WORKDIR /src

COPY app/Server/InventoryDemo.Server.csproj app/Server/
RUN dotnet restore app/Server/InventoryDemo.Server.csproj

COPY app/Server/ app/Server/
COPY db/ db/
COPY --from=client-build /src/app/ClientApp/dist/ app/Server/wwwroot/
RUN dotnet publish app/Server/InventoryDemo.Server.csproj -c Release -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS runtime

WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates \
    && rm -rf /var/lib/apt/lists/*

COPY --from=server-build /out/ ./
COPY db/ ./db/
COPY SYSTEM_PROMPT.md ./SYSTEM_PROMPT.md
COPY HELLO_PROMPT.md ./HELLO_PROMPT.md
COPY FEW_SHOT_PROMPTS.json ./FEW_SHOT_PROMPTS.json

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=5 \
  CMD curl -fsS http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "InventoryDemo.Server.dll"]
