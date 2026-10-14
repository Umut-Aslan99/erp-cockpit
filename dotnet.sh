#!/usr/bin/env bash
# Runs the .NET SDK in Docker (no SDK on the host). Usage: ./dotnet.sh build Einsatz.slnx
set -euo pipefail
docker run --rm ${DOTNET_DOCKER_ARGS:-} -u "$(id -u):$(id -g)" -e HOME=/tmp -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e DOTNET_NOLOGO=1 \
  -e NUGET_PACKAGES=/nuget -v erp-nuget:/nuget -v "$(cd "$(dirname "$0")" && pwd)/api:/src" -w /src \
  mcr.microsoft.com/dotnet/sdk:10.0 dotnet "$@"
