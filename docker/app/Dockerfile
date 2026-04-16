FROM node:20-bookworm-slim AS client-build
WORKDIR /src/client

COPY client/package*.json ./
RUN npm install

COPY client/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS server-build
WORKDIR /src

COPY server/InventoryDemo.Server.csproj server/
RUN dotnet restore server/InventoryDemo.Server.csproj

COPY server/ server/
COPY db/ db/
COPY --from=client-build /src/client/dist/ server/wwwroot/
RUN dotnet publish server/InventoryDemo.Server.csproj -c Release -o /out /p:UseAppHost=false

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
