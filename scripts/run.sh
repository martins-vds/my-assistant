#!/usr/bin/env bash
set -euo pipefail

# Auto-detect audio: try a short recording to see if the default PCM device works
if timeout 1 arecord -q -r 16000 -c 1 -f S16_LE -t raw -d 0 /dev/null 2>/dev/null; then
    echo "Audio device detected — starting in voice mode"
    exec dotnet run --project ../src/FocusAssistant.Cli -- "$@"
else
    echo "No usable audio device found — starting in text mode"
    exec dotnet run --project ../src/FocusAssistant.Cli -- --text "$@"
fi