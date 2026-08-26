#!/usr/bin/env bash
# Optional: install Wine for running Windows MajdataEdit builds on Linux.
set -euo pipefail
export DEBIAN_FRONTEND=noninteractive
sudo dpkg --add-architecture i386 2>/dev/null || true
sudo apt-get update -qq
sudo apt-get install -y --no-install-recommends wine64 wine32 winbind cabextract
echo "Wine installed. Initialize prefix once with: WINEPREFIX=~/.wine-majdata WINEDLLOVERRIDES=mscoree,mshtml=d wineboot -i"
