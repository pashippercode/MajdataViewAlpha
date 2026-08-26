#!/usr/bin/env bash
# Assemble a Linux release directory under dist/linux/ (no WPF / Windows binaries).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VENV="${MAJDATA_PYTHON_VENV:-$HOME/.venvs/majdataviewalpha}"
VERSION="${MAJDATA_VERSION:-$(git -C "$ROOT" describe --tags --always --dirty 2>/dev/null || echo dev)}"
OUT="$ROOT/dist/linux/MajdataViewAlpha-Linux-$VERSION"
ARCHIVE="$ROOT/dist/linux/MajdataViewAlpha-Linux-$VERSION.tar.gz"

if [[ ! -x "$VENV/bin/python" ]]; then
  echo "Run scripts/linux-install.sh first" >&2
  exit 1
fi

bash "$ROOT/scripts/smoke-linux.sh"

rm -rf "$OUT"
mkdir -p "$OUT/bin" "$OUT/tools" "$OUT/App/MajdataView"

echo "$VERSION" > "$OUT/VERSION"
cp "$ROOT/README-LINUX.md" "$OUT/README.md"
cp "$ROOT/simai_parser.py" "$OUT/"

# Maicaiyin engine (source + model; runtime uses venv on target machine)
mkdir -p "$OUT/tools/Maicaiyin"
cp "$ROOT/MajdataEdit/tools/Maicaiyin/infer.py" "$OUT/tools/Maicaiyin/"
cp "$ROOT/MajdataEdit/tools/Maicaiyin/joint-placement-numpy.npz" "$OUT/tools/Maicaiyin/"
cp -r "$ROOT/MajdataEdit/tools/Maicaiyin/maicaiyin" "$OUT/tools/Maicaiyin/"
cp "$ROOT/MajdataEdit/tools/Maicaiyin/requirements.txt" "$OUT/tools/Maicaiyin/"
cp "$ROOT/MajdataEdit/tools/Maicaiyin/README.md" "$OUT/tools/Maicaiyin/" 2>/dev/null || true

# Shared assets
if [[ -d "$ROOT/Skin" ]]; then
  cp -r "$ROOT/Skin" "$OUT/"
fi
if [[ -d "$ROOT/Assets/StreamingAssets" ]]; then
  mkdir -p "$OUT/Assets/StreamingAssets"
  shopt -s nullglob
  for item in "$ROOT/Assets/StreamingAssets"/*; do
    base="$(basename "$item")"
    [[ "$base" == "ffmpeg.exe" || "$base" == "ffmpeg.exe.meta" ]] && continue
    cp -a "$item" "$OUT/Assets/StreamingAssets/"
  done
  shopt -u nullglob
fi

# Optional: drop in a prebuilt Unity Linux player
if [[ -n "${MAJDATA_LINUX_VIEW:-}" && -d "$MAJDATA_LINUX_VIEW" ]]; then
  cp -a "$MAJDATA_LINUX_VIEW/." "$OUT/App/MajdataView/"
  echo "Bundled Unity player from MAJDATA_LINUX_VIEW"
else
  cat > "$OUT/App/MajdataView/README.txt" <<'TXT'
Build MajdataView for Linux with Unity 6000.4.2f1 (Standalone Linux x86_64),
then copy the player output here or set MAJDATA_LINUX_VIEW when running
scripts/linux-release.sh.
TXT
fi

cat > "$OUT/bin/maicaiyin-infer" <<'WRAP'
#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VENV="${MAJDATA_PYTHON_VENV:-$HOME/.venvs/majdataviewalpha}"
exec "$VENV/bin/python" "$ROOT/tools/Maicaiyin/infer.py" "$@"
WRAP
chmod +x "$OUT/bin/maicaiyin-infer"

cat > "$OUT/bin/setup-python-env" <<WRAP
#!/usr/bin/env bash
set -euo pipefail
VENV="\${MAJDATA_PYTHON_VENV:-\$HOME/.venvs/majdataviewalpha}"
python3 -m venv "\$VENV"
"\$VENV/bin/pip" install --upgrade pip
"\$VENV/bin/pip" install -r "\$(cd "\$(dirname "\$0")/.." && pwd)/tools/Maicaiyin/requirements.txt"
echo "Python env ready: \$VENV"
WRAP
chmod +x "$OUT/bin/setup-python-env"

mkdir -p "$(dirname "$ARCHIVE")"
tar -C "$(dirname "$OUT")" -czf "$ARCHIVE" "$(basename "$OUT")"

echo "Release directory: $OUT"
echo "Archive:           $ARCHIVE"
du -sh "$OUT" "$ARCHIVE"
