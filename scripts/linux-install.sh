#!/usr/bin/env bash
# Idempotent Linux development bootstrap for MajdataViewAlpha.
set -euo pipefail

export DEBIAN_FRONTEND=noninteractive
export DOTNET_CLI_TELEMETRY_OPTOUT=1

sudo apt-get update -qq
sudo apt-get install -y --no-install-recommends \
  python3-venv python3-pip libsndfile1 libsndfile1-dev \
  dotnet-sdk-8.0 ffmpeg git curl

VENV="${MAJDATA_PYTHON_VENV:-$HOME/.venvs/majdataviewalpha}"
python3 -m venv "$VENV"
"$VENV/bin/pip" install --upgrade pip
"$VENV/bin/pip" install -r MajdataEdit/tools/Maicaiyin/requirements.txt

"$VENV/bin/python" -c "import numpy, librosa, scipy, soundfile; assert numpy.__version__.startswith('2.3'); print('maicaiyin-deps-ok', numpy.__version__)"

echo "Linux dev environment ready."
echo "  Python venv: $VENV"
echo "  Activate:    source \"$VENV/bin/activate\""
echo "  Optional GUI: bash scripts/wine-install.sh  # run Windows MajdataEdit under Wine"
