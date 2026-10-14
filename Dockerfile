# syntax=docker/dockerfile:1

# ── 1. React app: type check, unit tests, production build ──────────────
FROM node:22-alpine AS web
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY web/ .
RUN npx tsc -b && npx vitest run && npx vite build

# ── 2. ASP.NET Core API, with the React build as its wwwroot ────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY api/global.json api/Directory.Build.props api/.editorconfig ./
COPY api/src/Erp.Core/Erp.Core.csproj src/Erp.Core/
COPY api/src/Erp.Api/Erp.Api.csproj src/Erp.Api/
RUN dotnet restore src/Erp.Api/Erp.Api.csproj
COPY api/src/ src/
COPY --from=web /web/dist src/Erp.Api/wwwroot/
RUN dotnet publish src/Erp.Api/Erp.Api.csproj -c Release -o /out --no-restore

# ── 3. Runtime: one small container serves app + API ────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
# German number/date formats need ICU; "today" means Europe/Berlin.
RUN apk add --no-cache icu-libs icu-data-full tzdata krb5-libs
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false TZ=Europe/Berlin ASPNETCORE_HTTP_PORTS=8080 DOTNET_gcServer=0
WORKDIR /app
COPY --from=api /out .
USER app
HEALTHCHECK --interval=30s --timeout=3s --start-period=30s --start-interval=2s CMD wget -qO- http://127.0.0.1:8080/api/health >/dev/null || exit 1
ENTRYPOINT ["dotnet", "Erp.Api.dll"]
