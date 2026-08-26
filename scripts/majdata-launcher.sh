#!/usr/bin/env bash
# Linux-native launcher: View player, Avalonia editor, or CLI tooling.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VIEW="${MAJDATA_VIEW:-$ROOT/App/MajdataView}"
EDIT="${MAJDATA_EDIT:-$ROOT/App/MajdataEdit/MajdataEdit}"
CLI="$ROOT/bin/majdata"

view_bin=""
if [[ -d "$VIEW" ]]; then
  view_bin="$(find "$VIEW" -maxdepth 1 -type f \( -name '*.x86_64' -o -name 'MajdataView*' \) -perm -111 2>/dev/null | head -1)"
fi

echo "MajdataViewAlpha Linux launcher"
echo "  View:   ${view_bin:-not built — use Unity Standalone Linux64}"
echo "  Edit:   ${EDIT} (Avalonia)"
echo "  CLI:    ${CLI:-$ROOT/bin/majdata after release build}"

if [[ -n "$view_bin" ]]; then
  echo "Starting View: $view_bin"
  exec "$view_bin" "$@"
fi

if [[ -x "$EDIT" ]]; then
  export MAJDATA_ROOT="${MAJDATA_ROOT:-$ROOT}"
  export MAJDATA_PYTHON="${MAJDATA_PYTHON:-${MAJDATA_PYTHON_VENV:-$HOME/.venvs/majdataviewalpha}/bin/python}"
  echo "Starting Edit: $EDIT"
  exec "$EDIT" "$@"
fi

if [[ -x "$ROOT/bin/majdata" ]]; then
  exec "$ROOT/bin/majdata" doctor
fi

echo "Nothing to launch. Run ./scripts/linux-release.sh or build Unity View first." >&2
exit 1
