#!/usr/bin/env bash
# Runs the .NET SDK in Docker (no SDK on the host). Usage: ./dotnet.sh build Einsatz.slnx
set -euo pipefail
# A new cache volume belongs to root (fresh machine, CI runner): hand it to the calling user first.
docker run --rm -u 0 --entrypoint sh -v erp-nuget:/nuget mcr.microsoft.com/dotnet/sdk:10.0 \
  -c 'u=$1 g=$2; shift 2; for d; do [ "$(stat -c %u "$d")" = "$u" ] || chown -R "$u:$g" "$d"; done' sh "$(id -u)" "$(id -g)" /nuget
docker run --rm ${DOTNET_DOCKER_ARGS:-} -u "$(id -u):$(id -g)" -e HOME=/tmp -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e DOTNET_NOLOGO=1 \
  -e NUGET_PACKAGES=/nuget -v erp-nuget:/nuget -v "$(cd "$(dirname "$0")" && pwd)/api:/src" -w /src \
  mcr.microsoft.com/dotnet/sdk:10.0 dotnet "$@"
