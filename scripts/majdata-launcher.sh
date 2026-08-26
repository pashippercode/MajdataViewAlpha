#!/usr/bin/env bash
# Linux-native launcher: View player (if built) + CLI tooling; Edit GUI via Wine when available.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VIEW="${MAJDATA_VIEW:-$ROOT/App/MajdataView}"
CLI="$ROOT/bin/majdata"

view_bin=""
if [[ -d "$VIEW" ]]; then
  view_bin="$(find "$VIEW" -maxdepth 1 -type f \( -name '*.x86_64' -o -name 'MajdataView*' \) -perm -111 2>/dev/null | head -1)"
fi

echo "MajdataViewAlpha Linux launcher"
echo "  CLI:    ${CLI:-$ROOT/scripts/use native majdata CLI after release build}"
echo "  View:   ${view_bin:-not built — use Unity Standalone Linux64}"
echo "  Edit:   native CLI (majdata auto-onset) or Wine (scripts/run-edit-wine.sh)"

if [[ -n "$view_bin" ]]; then
  echo "Starting View: $view_bin"
  exec "$view_bin" "$@"
fi

if [[ -x "$ROOT/bin/majdata" ]]; then
  exec "$ROOT/bin/majdata" doctor
fi

echo "Nothing to launch. Run ./scripts/linux-release.sh or build Unity View first." >&2
exit 1
