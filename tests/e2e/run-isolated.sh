#!/bin/sh
set -eu
test -n "$E2E_AUTH_CONNECTION"
repo=$(git rev-parse --show-toplevel)
results="$repo/coverage/e2e-isolated"
network=$(printenv E2E_DOCKER_NETWORK || true)
test -n "$network" || network=asg-v2_default
base=$(printenv E2E_BASE_URL || true)
test -n "$base" || base=http://modern:8080
mkdir -p "$results" "$repo/coverage/playwright"
cleanup() {
  if [ -f "$results/admin.json" ]; then
    docker run --rm --network "$network" -e E2E_AUTH_CONNECTION \
      -v "$results:/results" mcr.microsoft.com/dotnet/sdk:10.0 \
      dotnet /results/fixture/Moodle.E2eFixtures.dll /results/admin.json
  fi
}
trap cleanup EXIT
docker run --rm --network "$network" -e E2E_AUTH_CONNECTION \
  -v "$repo:/source:ro" -v "$results:/results" mcr.microsoft.com/dotnet/sdk:10.0 sh -c '
    set -eu
    mkdir -p /tmp/work
    cd /source
    tar --exclude=.git --exclude=coverage --exclude=node_modules --exclude=bin --exclude=obj -cf - . | tar -xf - -C /tmp/work
    cd /tmp/work
    dotnet build tests/Moodle.E2eFixtures -c Release -o /results/fixture
    if [ -f /results/admin.json ]; then
      dotnet /results/fixture/Moodle.E2eFixtures.dll /results/admin.json
    fi
    dotnet /results/fixture/Moodle.E2eFixtures.dll /results/admin.json
  '
docker run --rm --network "$network" \
  -e E2E_BASE_URL="$base" -e E2E_ADMIN_FIXTURE=/fixtures/admin.json \
  -v "$repo:/work:ro" -v "$results:/fixtures:ro" \
  -v "$repo/coverage/playwright:/work/coverage/playwright" -w /work/tests/e2e \
  mcr.microsoft.com/playwright:v1.56.1-noble npx playwright test