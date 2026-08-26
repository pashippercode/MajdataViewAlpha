#!/usr/bin/env bash
# Smoke-test Linux-runnable components (Maicaiyin + simai_parser).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VENV="${MAJDATA_PYTHON_VENV:-$HOME/.venvs/majdataviewalpha}"
PYTHON="$VENV/bin/python"
WORKDIR="${MAJDATA_SMOKE_DIR:-/tmp/majdata-smoke}"
DEMO_DIR="$WORKDIR/demo"

if [[ ! -x "$PYTHON" ]]; then
  echo "Missing venv at $VENV — run scripts/linux-install.sh first" >&2
  exit 1
fi

rm -rf "$DEMO_DIR"
mkdir -p "$DEMO_DIR"

# 8s 120 BPM metronome WAV
"$PYTHON" - <<'PY'
import math, struct, wave
path = "/tmp/majdata-smoke/demo/metronome.wav"
sr, duration, bpm = 44100, 8.0, 120.0
n = int(sr * duration)
samples = [0.0] * n
click_len = int(0.03 * sr)
t = 0.0
while t < duration:
    start = int(t * sr)
    for i in range(click_len):
        idx = start + i
        if idx >= n:
            break
        samples[idx] = math.sin(2 * math.pi * 1000 * i / sr) * (1.0 - i / click_len) * 0.6
    t += 60.0 / bpm
with wave.open(path, "w") as w:
    w.setnchannels(1)
    w.setsampwidth(2)
    w.setframerate(sr)
    w.writeframes(b"".join(struct.pack("<h", max(-32767, min(32767, int(s * 32767)))) for s in samples))
print("wrote", path)
PY

echo "== Maicaiyin infer =="
"$PYTHON" "$ROOT/MajdataEdit/tools/Maicaiyin/infer.py" \
  "$DEMO_DIR/metronome.wav" \
  --output "$DEMO_DIR/onset" \
  --bpm 120 --offset 0 --title SmokeTest --level 10

echo "== simai_parser =="
"$PYTHON" - <<PY
import json, sys
sys.path.insert(0, "$ROOT")
from simai_parser import maidata_to_majson, save_temp_majson

maidata = open("$DEMO_DIR/onset/maidata.txt", encoding="utf-8").read()
majson = maidata_to_majson(maidata, "1")
assert majson is not None, "parse failed"
notes = sum(len(t["noteList"]) for t in majson["timingList"])
assert notes > 0, "no notes"
save_temp_majson(majson, "$DEMO_DIR/parsed")
print("parsed notes:", notes)
print("smoke-ok")
PY

echo "All Linux smoke checks passed."
