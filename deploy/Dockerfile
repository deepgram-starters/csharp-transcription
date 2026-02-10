# Stage 1: Build custom Caddy with rate limiting module
FROM golang:1.25-alpine AS caddy-builder
WORKDIR /build
RUN go install github.com/caddyserver/xcaddy/cmd/xcaddy@latest
RUN xcaddy build --with github.com/mholt/caddy-ratelimit
# Binary output: /build/caddy

# Stage 2: Build frontend
FROM node:24-slim AS frontend-builder
RUN corepack enable
WORKDIR /build
COPY frontend/package.json frontend/pnpm-lock.yaml ./frontend/
RUN cd frontend && pnpm install --frozen-lockfile
COPY frontend/ ./frontend/
RUN cd frontend && pnpm build
# Inject Caddy template directives for subpath base path + session nonce
RUN sed -i 's|<head>|<head>\n  <base href="{{ .Req.Header.Get "X-Base-Path" }}">\n  <meta name="session-nonce" content="{{ placeholder "http.request.uuid" }}">|' ./frontend/dist/index.html

# Stage 3: Build .NET app
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS dotnet-builder
WORKDIR /build
COPY *.csproj ./
RUN dotnet restore
COPY *.cs deepgram.toml sample.env ./
RUN dotnet publish -c Release -o /build/out

# Stage 4: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0

# Copy custom Caddy binary
COPY --from=caddy-builder /build/caddy /usr/bin/caddy

WORKDIR /app

# Copy published .NET app
COPY --from=dotnet-builder /build/out/ ./
COPY --from=dotnet-builder /build/deepgram.toml /build/sample.env ./

# Copy built frontend
COPY --from=frontend-builder /build/frontend/dist/ ./frontend/dist/

# Copy shared deployment files
COPY deploy/Caddyfile /etc/caddy/Caddyfile
COPY deploy/start.sh ./start.sh
RUN chmod +x ./start.sh

ENV ASPNETCORE_URLS=http://0.0.0.0:8081
EXPOSE 8080

ARG DOTNET_DLL=csharp-transcription.dll
ENV BACKEND_CMD="dotnet ${DOTNET_DLL}"
CMD ["./start.sh"]
