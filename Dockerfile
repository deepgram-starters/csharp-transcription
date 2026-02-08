# Stage 1: Build frontend
FROM node:24-slim AS frontend-builder
RUN corepack enable
WORKDIR /build
COPY frontend/package.json frontend/pnpm-lock.yaml ./frontend/
RUN cd frontend && pnpm install --frozen-lockfile
COPY frontend/ ./frontend/
RUN cd frontend && pnpm build

# Stage 2: Build .NET app
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS dotnet-builder
WORKDIR /build
COPY *.csproj ./
RUN dotnet restore
COPY *.cs deepgram.toml sample.env ./
RUN dotnet publish -c Release -o /build/out

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0

# Install Caddy
RUN apt-get update && \
    apt-get install -y debian-keyring debian-archive-keyring apt-transport-https curl && \
    curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg && \
    curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | tee /etc/apt/sources.list.d/caddy-stable.list && \
    apt-get update && \
    apt-get install -y caddy && \
    apt-get clean && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Copy published .NET app
COPY --from=dotnet-builder /build/out/ ./
COPY --from=dotnet-builder /build/deepgram.toml /build/sample.env ./

# Copy built frontend
COPY --from=frontend-builder /build/frontend/dist/ ./frontend/dist/

# Copy shared deployment files
COPY Caddyfile /etc/caddy/Caddyfile
COPY start.sh ./start.sh
RUN chmod +x ./start.sh

ENV ASPNETCORE_URLS=http://0.0.0.0:8081
EXPOSE 8080

ARG DOTNET_DLL=csharp-transcription.dll
ENV BACKEND_CMD="dotnet ${DOTNET_DLL}"
CMD ["./start.sh"]
