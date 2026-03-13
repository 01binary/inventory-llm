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

ARG TARGETARCH
ARG PIPER_VERSION=2023.11.14-2

WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates libstdc++6 xz-utils \
    && rm -rf /var/lib/apt/lists/*

RUN case "${TARGETARCH}" in \
      "amd64") export PIPER_ARCH="x86_64" ;; \
      "arm64") export PIPER_ARCH="aarch64" ;; \
      *) echo "Unsupported TARGETARCH: ${TARGETARCH}" && exit 1 ;; \
    esac \
    && curl -fsSL "https://github.com/rhasspy/piper/releases/download/v${PIPER_VERSION}/piper_linux_${PIPER_ARCH}.tar.gz" -o /tmp/piper.tar.gz \
    && mkdir -p /usr/local/bin/piper \
    && tar -xzf /tmp/piper.tar.gz -C /usr/local/bin/piper --strip-components=1 \
    && rm /tmp/piper.tar.gz \
    && chmod +x /usr/local/bin/piper/piper

COPY --from=server-build /out/ ./
COPY db/ ./db/

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=5 \
  CMD curl -fsS http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "InventoryDemo.Server.dll"]
