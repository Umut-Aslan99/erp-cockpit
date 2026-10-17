#!/usr/bin/env bash
# react.umut-aslan.dev (ERP-Cockpit):  ./ops.sh test | deploy | logs
set -euo pipefail
cd "$(dirname "$0")"
export GIT_SHA="${GIT_SHA:-$(git rev-parse --short HEAD 2>/dev/null || echo local)}"
PW_IMAGE="mcr.microsoft.com/playwright:v1.64.0-noble"

backend_tests() {   # xUnit: core + API against a throw-away PostgreSQL, in Berlin time
  docker network create erp-unit >/dev/null 2>&1 || true
  docker rm -f erp-unit-db >/dev/null 2>&1 || true
  docker run -d --name erp-unit-db --network erp-unit -e POSTGRES_USER=erp -e POSTGRES_PASSWORD=erp postgres:17-alpine >/dev/null
  trap 'docker rm -f erp-unit-db >/dev/null 2>&1 || true' RETURN
  until docker exec erp-unit-db pg_isready -U erp >/dev/null 2>&1; do sleep 1; done
  DOTNET_DOCKER_ARGS="--network erp-unit -e TZ=Europe/Berlin -e ERP_TEST_DB=Host=erp-unit-db;Username=erp;Password=erp" ./dotnet.sh test --solution Erp.slnx
}

e2e_tests() {       # Playwright against the full stack, isolated from production (web unit tests run in the image build)
  # The compose file joins Traefik's external network: it exists on the server; elsewhere (CI, a fresh clone) an
  # empty one stands in, so the stack can start (Traefik itself is off here).
  docker network inspect proxy >/dev/null 2>&1 || docker network create proxy >/dev/null
  TRAEFIK_ENABLE=false docker compose -p erp-ci up -d --build --wait
  trap 'docker compose -p erp-ci down -v >/dev/null 2>&1 || true' RETURN
  local app; app=$(docker compose -p erp-ci ps -q app)
  docker run --rm -u "$(id -u):$(id -g)" -e HOME=/tmp -v "$PWD/e2e:/app" -w /app node:22-alpine npm ci --no-audit --no-fund --ignore-scripts --silent
  docker run --rm --ipc=host --network "container:$app" -u "$(id -u):$(id -g)" -e HOME=/tmp -e CI=1 -v "$PWD/e2e:/app" -w /app "$PW_IMAGE" npx playwright test
}

test()   { backend_tests; e2e_tests; }
deploy() { test; docker compose -p erp-cockpit up -d --build --wait --remove-orphans; docker compose -p erp-cockpit ps; }
logs()   { docker compose -p erp-cockpit logs -f --tail=100; }

"${1:-test}"
