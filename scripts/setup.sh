#!/usr/bin/env bash
set -euo pipefail

VOSK_MODEL_NAME="vosk-model-small-en-us-0.15"
VOSK_MODEL_URL="https://alphacephei.com/vosk/models/${VOSK_MODEL_NAME}.zip"
MODEL_DIR="$HOME/.focus-assistant/models"
MODEL_PATH="${MODEL_DIR}/${VOSK_MODEL_NAME}"

echo "=== Focus Assistant Setup ==="
echo ""

# --- 1. espeak-ng ---
if command -v espeak-ng &>/dev/null; then
    echo "[OK] espeak-ng is already installed ($(espeak-ng --version 2>&1 | head -1))"
else
    echo "[*] Installing espeak-ng..."
    if command -v apt-get &>/dev/null; then
        sudo apt-get update -qq && sudo apt-get install -y -qq espeak-ng
    elif command -v dnf &>/dev/null; then
        sudo dnf install -y espeak-ng
    elif command -v pacman &>/dev/null; then
        sudo pacman -S --noconfirm espeak-ng
    else
        echo "[ERROR] Could not detect package manager. Install espeak-ng manually."
        exit 1
    fi
    echo "[OK] espeak-ng installed"
fi

# --- 2. alsa-utils (arecord for audio capture) ---
if command -v arecord &>/dev/null; then
    echo "[OK] arecord is already installed (alsa-utils)"
else
    echo "[*] Installing alsa-utils (for arecord audio capture)..."
    if command -v apt-get &>/dev/null; then
        sudo apt-get update -qq && sudo apt-get install -y -qq alsa-utils
    elif command -v dnf &>/dev/null; then
        sudo dnf install -y alsa-utils
    elif command -v pacman &>/dev/null; then
        sudo pacman -S --noconfirm alsa-utils
    else
        echo "[ERROR] Could not detect package manager. Install alsa-utils manually."
        exit 1
    fi
    echo "[OK] alsa-utils installed"
fi

# --- 3. Vosk model ---
if [[ -d "$MODEL_PATH" ]]; then
    echo "[OK] Vosk model already exists at ${MODEL_PATH}"
else
    echo "[*] Downloading Vosk model: ${VOSK_MODEL_NAME}..."
    mkdir -p "$MODEL_DIR"

    TMPZIP=$(mktemp /tmp/vosk-model-XXXXXX.zip)
    trap 'rm -f "$TMPZIP"' EXIT

    if command -v wget &>/dev/null; then
        wget -q --show-progress -O "$TMPZIP" "$VOSK_MODEL_URL"
    elif command -v curl &>/dev/null; then
        curl -L --progress-bar -o "$TMPZIP" "$VOSK_MODEL_URL"
    else
        echo "[ERROR] Neither wget nor curl found. Install one and retry."
        exit 1
    fi

    echo "[*] Extracting model to ${MODEL_DIR}..."
    python3 -c "import zipfile, sys; zipfile.ZipFile(sys.argv[1]).extractall(sys.argv[2])" "$TMPZIP" "$MODEL_DIR"
    rm -f "$TMPZIP"

    if [[ -d "$MODEL_PATH" ]]; then
        echo "[OK] Vosk model installed at ${MODEL_PATH}"
    else
        echo "[ERROR] Extraction succeeded but model directory not found at ${MODEL_PATH}"
        echo "       Check contents of ${MODEL_DIR} and rename if needed."
        exit 1
    fi
fi

# --- 3. .NET SDK check ---
if command -v dotnet &>/dev/null; then
    echo "[OK] .NET SDK found ($(dotnet --version))"
else
    echo "[WARN] .NET SDK not found. Install .NET 8+ from https://dotnet.microsoft.com/download"
fi

echo ""
echo "=== Setup complete! ==="
echo "Run your assistant with:  ./run.sh"
