#!/usr/bin/env bash
#
# sonar-scan.sh - runs Clippy and test coverage for the easy-git Rust workspace
# on this machine, then sends everything to a local SonarQube server through
# the scanner container. Linux/macOS counterpart of scripts/sonar-scan.ps1.
#
# Steps:
#   1. cargo clippy   -> clippy.json
#   2. cargo llvm-cov -> lcov.info   (absolute paths rewritten to /usr/src)
#   3. sonarsource/sonar-scanner-cli container on the docker-compose network,
#      using the settings in sonar-project.properties.
#
# Usage:
#   SONAR_TOKEN=squ_... ./scripts/sonar-scan.sh
#   ./scripts/sonar-scan.sh --skip-coverage
#
# Environment (all optional except the token):
#   SONAR_TOKEN           User, Global Analysis or Project Analysis token
#   SONAR_HOST_URL        server as seen from the container (default http://sonarqube:9000)
#   SONAR_DOCKER_NETWORK  compose network (default northwind-platform_northwind-net,
#                         empty string = do not pass --network)

set -euo pipefail

HOST_URL="${SONAR_HOST_URL:-http://sonarqube:9000}"
NETWORK="${SONAR_DOCKER_NETWORK-northwind-platform_northwind-net}"
SKIP_CLIPPY=0
SKIP_COVERAGE=0

for arg in "$@"; do
  case "$arg" in
    --skip-clippy)   SKIP_CLIPPY=1 ;;
    --skip-coverage) SKIP_COVERAGE=1 ;;
    -h|--help)       sed -n '2,22p' "$0"; exit 0 ;;
    *) echo "error: unknown option '$arg'" >&2; exit 2 ;;
  esac
done

step() { printf '\n\033[36m==> %s\033[0m\n' "$1"; }
fail() { echo "error: $1" >&2; exit 1; }

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

step "Checking prerequisites"
[ -f sonar-project.properties ] || fail "sonar-project.properties not found in $REPO_ROOT."
[ -n "${SONAR_TOKEN:-}" ]       || fail "no token. Run with SONAR_TOKEN=... set."
command -v cargo  >/dev/null || fail "'cargo' is not on PATH."
command -v docker >/dev/null || fail "'docker' is not on PATH."
if [ "$SKIP_COVERAGE" -eq 0 ] && ! cargo llvm-cov --version >/dev/null 2>&1; then
  fail "cargo-llvm-cov is missing. Install it once with:
  rustup component add llvm-tools-preview
  cargo install cargo-llvm-cov"
fi
if [ -n "$NETWORK" ] && ! docker network inspect "$NETWORK" >/dev/null 2>&1; then
  fail "docker network '$NETWORK' not found. Is docker-compose up? (docker network ls)"
fi

if [ "$SKIP_CLIPPY" -eq 0 ]; then
  step "cargo clippy -> clippy.json"
  cargo clippy --workspace --all-targets --message-format=json > clippy.json
  echo "Clippy messages in report: $(grep -c '"reason":"compiler-message"' clippy.json || true)"
fi

if [ "$SKIP_COVERAGE" -eq 0 ]; then
  step "cargo llvm-cov -> lcov.info"
  cargo llvm-cov --workspace --lcov --output-path lcov.info

  # SF:/home/me/easy-git/crates/x/src/lib.rs  ->  SF:/usr/src/crates/x/src/lib.rs
  awk -v prefix="SF:$REPO_ROOT/" '
    index($0, prefix) == 1 { $0 = "SF:/usr/src/" substr($0, length(prefix) + 1) }
    { print }
  ' lcov.info > lcov.info.tmp
  mv lcov.info.tmp lcov.info

  unmapped="$(grep '^SF:' lcov.info | grep -vc '^SF:/usr/src/' || true)"
  if [ "$unmapped" -gt 0 ]; then
    echo "warning: $unmapped file(s) in lcov.info are outside the repository and will be ignored." >&2
  fi
fi

step "Running sonar-scanner-cli in Docker"
docker_args=(run --rm)
[ -n "$NETWORK" ] && docker_args+=(--network "$NETWORK")
# The token is passed by name so it never shows up in the process list.
export SONAR_TOKEN
docker_args+=(
  -e "SONAR_HOST_URL=$HOST_URL"
  -e SONAR_TOKEN
  -v "$REPO_ROOT:/usr/src"
  sonarsource/sonar-scanner-cli
)
docker "${docker_args[@]}"

project_key="$(sed -n 's/^sonar\.projectKey=//p' sonar-project.properties)"
printf '\n\033[32mDone. Results: http://localhost:9000/dashboard?id=%s\033[0m\n' "$project_key"
