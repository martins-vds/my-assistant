#!/usr/bin/env bash
set -euo pipefail

# ── Focus Assistant — Single-File Executable Builder ──
#
# Usage:
#   ./publish.sh              # Build for current OS
#   ./publish.sh linux        # Build for Linux x64
#   ./publish.sh windows      # Build for Windows x64
#   ./publish.sh both         # Build for both

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT="$REPO_ROOT/src/FocusAssistant.Cli/FocusAssistant.Cli.csproj"
OUT_DIR="$REPO_ROOT/publish"

# Vosk NuGet native lib directory (for cross-compile fixup)
NUGET_ROOT="${NUGET_PACKAGES:-$HOME/.nuget/packages}"
VOSK_LIB="$NUGET_ROOT/vosk/0.3.38/build/lib"

TARGET="${1:-current}"

# Fix Vosk native libs after publish.
# Vosk's .targets uses IsOsPlatform() which checks the BUILD machine OS,
# not the target RID. When cross-compiling (e.g. Linux→Windows), the wrong
# native library gets copied. This function removes incorrect libs and
# copies the correct ones from the NuGet cache.
fix_vosk_native_libs() {
    local rid="$1"
    local out="$2"

    echo "  Fixing Vosk native libraries for $rid..."

    # Remove any wrong-platform Vosk native libs placed by MSBuild
    rm -f "$out"/libvosk.so "$out"/libvosk.dylib
    rm -f "$out"/libvosk.dll "$out"/libgcc_s_seh-1.dll "$out"/libstdc++-6.dll "$out"/libwinpthread-1.dll

    # Copy the correct native libs for the target RID
    case "$rid" in
        win-x64)
            cp "$VOSK_LIB/win-x64/"*.dll "$out/"
            echo "  ✓ Copied Windows Vosk native DLLs"
            ;;
        linux-x64)
            cp "$VOSK_LIB/linux-x64/"*.so "$out/"
            echo "  ✓ Copied Linux Vosk native .so"
            ;;
        osx-*)
            cp "$VOSK_LIB/osx-universal/"*.dylib "$out/"
            echo "  ✓ Copied macOS Vosk native .dylib"
            ;;
    esac
}

publish_for() {
    local rid="$1"
    local label="$2"
    local out="$OUT_DIR/$rid"

    echo ""
    echo "── Building for $label ($rid) ──"
    mkdir -p "$out"

    dotnet publish "$PROJECT" \
        --configuration Release \
        --runtime "$rid" \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=false \
        -p:EnableCompressionInSingleFile=true \
        --output "$out"

    # Fix Vosk native libs for cross-compilation
    fix_vosk_native_libs "$rid" "$out"

    # Find the executable
    if [[ "$rid" == win-* ]]; then
        local exe="$out/FocusAssistant.Cli.exe"
    else
        local exe="$out/FocusAssistant.Cli"
    fi

    if [[ -f "$exe" ]]; then
        local size
        size=$(du -sh "$exe" | cut -f1)
        echo "✓ $label executable: $exe ($size)"
    else
        echo "✗ Build failed — executable not found at $exe"
        return 1
    fi
}

case "$TARGET" in
    linux|Linux)
        publish_for "linux-x64" "Linux x64"
        ;;
    windows|Windows|win)
        publish_for "win-x64" "Windows x64"
        ;;
    both|all)
        publish_for "linux-x64" "Linux x64"
        publish_for "win-x64" "Windows x64"
        ;;
    current)
        case "$(uname -s)" in
            Linux*)  publish_for "linux-x64" "Linux x64" ;;
            Darwin*) publish_for "osx-x64" "macOS x64" ;;
            MINGW*|MSYS*|CYGWIN*) publish_for "win-x64" "Windows x64" ;;
            *) echo "Unknown OS: $(uname -s). Specify 'linux' or 'windows'."; exit 1 ;;
        esac
        ;;
    *)
        echo "Usage: $0 [linux|windows|both]"
        exit 1
        ;;
esac

echo ""
echo "── Done! Executables are in $OUT_DIR ──"
echo ""
echo "Run with:  $OUT_DIR/<rid>/FocusAssistant.Cli [--text]"
