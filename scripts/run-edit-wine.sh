#!/usr/bin/env bash
# Run a Windows-published MajdataEdit folder under Wine (GUI, best-effort).
set -euo pipefail

EDIT_DIR="${1:-${MAJDATA_EDIT_WIN:-}}"
if [[ -z "$EDIT_DIR" || ! -d "$EDIT_DIR" ]]; then
  echo "Usage: MAJDATA_EDIT_WIN=/path/to/MajdataEdit $0" >&2
  echo "   or: $0 /path/to/MajdataEdit" >&2
  exit 1
fi

export WINEPREFIX="${WINEPREFIX:-$HOME/.wine-majdata}"
export WINEDLLOVERRIDES="${WINEDLLOVERRIDES:-mscoree,mshtml=d}"
export MAJDATA_PYTHON="${MAJDATA_PYTHON:-$HOME/.venvs/majdataviewalpha/bin/python}"

if [[ ! -f "$EDIT_DIR/MajdataEdit.exe" ]]; then
  echo "MajdataEdit.exe not found under $EDIT_DIR" >&2
  exit 1
fi

if ! command -v wine >/dev/null 2>&1; then
  echo "Wine is not installed. Run scripts/wine-install.sh first." >&2
  exit 1
fi

cd "$EDIT_DIR"
exec wine ./MajdataEdit.exe "$@"
