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
ARG PIPER_VERSION=1.4.1

WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates libstdc++6 python3 python3-pip python3-venv \
    && rm -rf /var/lib/apt/lists/*

RUN case "${TARGETARCH}" in \
      "amd64") export PIPER_WHEEL="piper_tts-${PIPER_VERSION}-cp39-abi3-manylinux_2_17_x86_64.manylinux2014_x86_64.manylinux_2_28_x86_64.whl" ;; \
      "arm64") export PIPER_WHEEL="piper_tts-${PIPER_VERSION}-cp39-abi3-manylinux_2_17_aarch64.manylinux2014_aarch64.manylinux_2_28_aarch64.whl" ;; \
      *) echo "Unsupported TARGETARCH: ${TARGETARCH}" && exit 1 ;; \
    esac \
    && python3 -m venv /opt/piper-venv \
    && /opt/piper-venv/bin/pip install --no-cache-dir --upgrade pip \
    && /opt/piper-venv/bin/pip install --no-cache-dir "https://github.com/OHF-Voice/piper1-gpl/releases/download/v${PIPER_VERSION}/${PIPER_WHEEL}" \
    && ln -sf /opt/piper-venv/bin/piper /usr/local/bin/piper \
    && test -x /usr/local/bin/piper

COPY --from=server-build /out/ ./
COPY db/ ./db/

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=5 \
  CMD curl -fsS http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "InventoryDemo.Server.dll"]
